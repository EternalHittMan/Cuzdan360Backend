using Cuzdan360Backend.Models.DTOs;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory; // 👈 Cache
using System.ServiceModel.Syndication; // 👈 RSS
using System.Xml; // 👈 RSS
using System.Text.RegularExpressions; // 👈 Resim ayıklamak için

namespace Cuzdan360Backend.Services
{
    public class NewsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsService> _logger;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "FinanceNewsCache_Duncom";
        
        // Dünya Gazetesi Ekonomi RSS
        // Alternatifler: 
        // "https://www.haberturk.com/rss/ekonomi.xml"
        // "https://www.ntv.com.tr/ekonomi.rss"
        // Feed Listesi
        private readonly List<string> _feedUrls = new()
        {
            "https://www.bloomberght.com/rss",
            "https://www.haberturk.com/rss/ekonomi.xml",
            "https://www.ntv.com.tr/ekonomi.rss"
        }; 

        public NewsService(HttpClient httpClient, ILogger<NewsService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<List<NewsArticleDto>> GetNewsAsync()
        {
            // 1. Önce cache'i kontrol et
            if (_cache.TryGetValue(CacheKey, out List<NewsArticleDto>? cachedNews) && cachedNews != null)
            {
                _logger.LogInformation("Haberler cache'den getirildi.");
                return cachedNews;
            }
            
            var allNews = new List<NewsArticleDto>();
            var tasks = _feedUrls.Select(FetchFeedAsync).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                allNews.AddRange(result);
            }

            // Tarihe göre yeniden sırala (en güncel en üstte)
            allNews = allNews.OrderByDescending(x => x.ParsedDate).Take(10).ToList();

            if (allNews.Count == 0)
            {
                 // Eğer hiç haber yoksa hata döndürülür
                 return new List<NewsArticleDto> { new("hata", "Haberler yüklenemedi.", "Sistem", "", null, "#") };
            }

            // Cache'e kaydet
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
            _cache.Set(CacheKey, allNews, cacheEntryOptions);

            return allNews;
        }

        private async Task<List<NewsArticleDto>> FetchFeedAsync(string url)
        {
            try
            {
                _logger.LogInformation("RSS çekiliyor: {Url}", url);
                // HTML içeriği yerine XML bekliyoruz, ama bazı sunucular User-Agent'a göre farklı davranabilir.
                // Yahoo bazen gzip döner, HttpClient otomatik handle eder genellikle.
                
                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RSS isteği başarısız: {Url} - {StatusCode}", url, response.StatusCode);
                    return new List<NewsArticleDto>();
                }

                // Stream'i string olarak oku ve temizle
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                var xmlContent = await reader.ReadToEndAsync();
                
                // BOM ve whitespace temizliği (Basit Trim yeterli olmayabilir, string başında görünmez karakterler olabilir)
                xmlContent = xmlContent.Trim().Replace((char)0xFEFF, ' '); // BOM check

                // XmlReader ayarları
                var settings = new XmlReaderSettings 
                { 
                    Async = true, 
                    DtdProcessing = DtdProcessing.Ignore, 
                    CheckCharacters = false,
                    IgnoreWhitespace = true,
                    IgnoreComments = true
                };
                
                using var stringReader = new StringReader(xmlContent);
                using var xmlReader = XmlReader.Create(stringReader, settings);
                
                var feed = SyndicationFeed.Load(xmlReader);
                if (feed == null) return new List<NewsArticleDto>();

                var trCulture = new CultureInfo("tr-TR");
                var sourceName = feed.Title?.Text ?? "Haber Kaynağı";
                
                // Bloomberg için özel isimlendirme
                if (url.Contains("bloomberg")) sourceName = "Bloomberg HT";
                if (url.Contains("haberturk")) sourceName = "Habertürk Ekonomi";
                if (url.Contains("ntv")) sourceName = "NTV Ekonomi";

                return feed.Items
                    .Take(5)
                    .Select(item => {
                         // Tarih parse etme
                         string dateStr = item.PublishDate.ToString("g", trCulture);
                         
                         return new NewsArticleDto(
                            item.Id ?? Guid.NewGuid().ToString(),
                            item.Title.Text,
                            sourceName,
                            dateStr,
                            ExtractImageUrl(item, url), // URL'e göre özelleştirilmiş resim çekme
                            item.Links.FirstOrDefault()?.Uri.ToString() ?? "#"
                        ) { ParsedDate = item.PublishDate.DateTime }; // Sıralama için ekstra property (DTO'da yoksa eklemeliyiz veya sıralamayı burada yapmalıyız)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Feed hatası: {Url}", url);
                 return new List<NewsArticleDto>();
            }
        }

        /// <summary>
        /// RSS 'item' içinden resim (enclosure) veya içerikten <img> tag'i ayıklar.
        /// </summary>
        private string? ExtractImageUrl(SyndicationItem item, string feedUrl)
        {
            try
            {
                // 1. Yöntem: Standart <enclosure> tag'i
                var enclosure = item.Links.FirstOrDefault(l => l.RelationshipType == "enclosure");
                if (enclosure != null && enclosure.MediaType.StartsWith("image/"))
                {
                    return enclosure.Uri.ToString();
                }

                // 2. Yöntem: İçerik (Summary/Content) içinden Regex ile <img> arama
                var content = item.Summary?.Text ?? (item.Content as TextSyndicationContent)?.Text;
                if (string.IsNullOrEmpty(content)) return null;

                var match = Regex.Match(content, "<img.+?src=[\"'](.+?)[\"'].*?>", RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value; // Yakalanan URL
                }

                // Yahoo Finance için özel kontrol (media:content)
                // SyndicationFeed standart olarak media taglerini ElementExtension içine atar
                if (feedUrl.Contains("yahoo"))
                {
                    var mediaContent = item.ElementExtensions
                        .FirstOrDefault(e => e.OuterName == "content" && e.OuterNamespace == "http://search.yahoo.com/mrss/");
                    
                    if (mediaContent != null)
                    {
                         // XElement parse
                         var element = mediaContent.GetObject<System.Xml.Linq.XElement>();
                         var urlAttribute = element.Attribute("url");
                         if (urlAttribute != null) return urlAttribute.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RSS'ten resim ayıklanırken hata oluştu.");
            }
            
            return null; // Resim bulunamadı
        }
    }
}