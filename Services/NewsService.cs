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
        private const string RssFeedUrl = "https://www.bloomberght.com/rss"; 

        public NewsService(HttpClient httpClient, ILogger<NewsService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Cuzdan360Backend/1.0");
        }

        public async Task<List<NewsArticleDto>> GetNewsAsync()
        {
            // 1. Önce cache'i kontrol et
            if (_cache.TryGetValue(CacheKey, out List<NewsArticleDto>? cachedNews) && cachedNews != null)
            {
                _logger.LogInformation("Haberler cache'den getirildi.");
                return cachedNews;
            }
            
            _logger.LogInformation("Cache boş, RSS feed'inden taze haberler çekiliyor: {RssFeedUrl}", RssFeedUrl);

            try
            {
                // 2. RSS feed'ini çek
                using var stream = await _httpClient.GetStreamAsync(RssFeedUrl);
                using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
                var feed = SyndicationFeed.Load(xmlReader);

                if (feed == null)
                {
                    throw new InvalidOperationException("RSS feed'i yüklenemedi.");
                }

                var trCulture = new CultureInfo("tr-TR");
                
                // 3. RSS verisini DTO'ya dönüştür
                var newsFeed = feed.Items
                    .Take(5) // Sadece ilk 5 haberi al
                    .Select(item => new NewsArticleDto(
                        item.Id ?? Guid.NewGuid().ToString(),
                        item.Title.Text,
                        feed.Title.Text, // "Dünya Gazetesi - Ekonomi"
                        item.PublishDate.ToString("g", trCulture), // "28.10.2025 10:30"
                        ExtractImageUrl(item), // Resim URL'sini ayıkla
                        item.Links.FirstOrDefault()?.Uri.ToString() ?? "#"
                    ))
                    .ToList();

                // 4. Gelen veriyi cache'e kaydet (15 dakika)
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                _cache.Set(CacheKey, newsFeed, cacheEntryOptions);
                
                return newsFeed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RSS verisi çekilirken hata oluştu.");
                return new List<NewsArticleDto> { new("hata", "Haber servisinde hata oluştu, lütfen daha sonra tekrar deneyin.", "Sistem", "", null, "#") };
            }
        }

        /// <summary>
        /// RSS 'item' içinden resim (enclosure) veya içerikten <img> tag'i ayıklar.
        /// </summary>
        private string? ExtractImageUrl(SyndicationItem item)
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RSS'ten resim ayıklanırken hata oluştu.");
            }
            
            return null; // Resim bulunamadı
        }
    }
}