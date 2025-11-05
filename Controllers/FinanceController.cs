using Microsoft.AspNetCore.Mvc;
using YahooFinanceApi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using Cuzdan360Backend.Models.DTOs; // 👈 DTO'ları kullanmak için eklendi
using Cuzdan360Backend.Services; // 👈 NewsService'i kullanmak için eklendi

namespace Cuzdan360Backend.Controllers
{
    // --- DTO (Data Transfer Objects) ---
    // Not: NewsArticleDto, Models/DTOs/NewsDtos.cs dosyasından geliyor.

    /// <summary>
    /// Kur verisi DTO'su
    /// </summary>
    public record CurrencyRateDto(string Pair, double Rate, double Change);
    
    /// <summary>
    /// Dashboard için gerekli tüm finansal verileri içeren ana DTO
    /// </summary>
    public record DashboardDataDto(List<CurrencyRateDto> CurrencyRates, List<NewsArticleDto> NewsFeed);


    // --- API CONTROLLER ---

    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 👈 Bu endpoint'lere sadece giriş yapmış kullanıcılar erişebilir
    public class FinanceController : ControllerBase
    {
        // Hangi sembolleri çekeceğimizi ve nasıl isimlendireceğimizi belirleyen harita
/// <summary>
/// Yahoo Finance sembollerini okunabilir isimlerle eşleştiren statik bir sözlük.
/// </summary>
private static readonly Dictionary<string, string> TickerMap = new()
{
    // --- TRY Pariteleri ve BIST (Sizin Girdileriniz) ---
    { "USDTRY=X", "USD/TRY" },
    { "EURTRY=X", "EUR/TRY" },
    { "GBPTRY=X", "GBP/TRY" },
    { "XAUTRY=X", "Gram Altın (TL)" }, // Altın/TRY, Gram Altın olarak da bilinir
    { "XU100.IS", "BIST 100 Endeksi" },

    // --- Majör Döviz Kurları ---
    { "EURUSD=X", "EUR/USD" },
    { "GBPUSD=X", "GBP/USD" },
    { "USDJPY=X", "USD/JPY" },
    { "USDCHF=X", "USD/CHF" },
    { "USDCAD=X", "USD/CAD" },
    { "AUDUSD=X", "AUD/USD" },

    // --- Popüler Kripto Paralar (USD Bazlı) ---
    { "BTC-USD", "Bitcoin (BTC/USD)" },
    { "ETH-USD", "Ethereum (ETH/USD)" },
    { "SOL-USD", "Solana (SOL/USD)" },
    { "XRP-USD", "Ripple (XRP/USD)" },
    { "BNB-USD", "Binance Coin (BNB/USD)" },
    { "DOGE-USD", "Dogecoin (DOGE/USD)" },

    // --- Başlıca Dünya Endeksleri ---
    { "XU030.IS", "BIST 30 Endeksi" },
    { "^GSPC", "S&P 500 (ABD)" },
    { "^DJI", "Dow Jones Industrial Average (ABD)" },
    { "^IXIC", "NASDAQ Composite (ABD)" },
    { "^GDAXI", "DAX (Almanya)" },
    { "^FTSE", "FTSE 100 (İngiltere)" },
    { "^N225", "Nikkei 225 (Japonya)" },
    { "^HSI", "Hang Seng (Hong Kong)" },
    { "000001.SS", "Shanghai Composite (Çin)" },

    // --- Başlıca Emtialar (USD Bazlı) ---
    { "GC=F", "Altın Vadeli (Gold Futures)" },
    { "SI=F", "Gümüş Vadeli (Silver Futures)" },
    { "CL=F", "Ham Petrol Vadeli (WTI Crude)" },
    { "BZ=F", "Brent Petrol Vadeli (Brent Crude)" },
    { "NG=F", "Doğal Gaz Vadeli (Natural Gas)" },
    { "XAUUSD=X", "Spot Altın/USD" }, // Spot piyasa
    { "XAGUSD=X", "Spot Gümüş/USD" }, // Spot piyasa

    // --- Popüler Hisseler (BIST) ---
    // (BIST hisseleri için sonuna ".IS" eklenir)
    { "THYAO.IS", "Türk Hava Yolları" },
    { "KCHOL.IS", "Koç Holding" },
    { "GARAN.IS", "Garanti Bankası" },
    { "BIMAS.IS", "Bim Mağazalar" },
    { "TUPRS.IS", "Tüpraş" },
    { "EREGL.IS", "Ereğli Demir Çelik" },
    { "SAHOL.IS", "Sabancı Holding" },
    { "SISE.IS", "Şişecam" },
    
    // --- Popüler Hisseler (ABD) ---
    // (ABD borsalarındaki hisseler genelde ek almaz)
    { "AAPL", "Apple Inc." },
    { "MSFT", "Microsoft Corp." },
    { "GOOGL", "Alphabet Inc. (Google)" },
    { "AMZN", "Amazon.com, Inc." },
    { "NVDA", "NVIDIA Corp." },
    { "TSLA", "Tesla, Inc." },
    { "META", "Meta Platforms, Inc." }
};
        private readonly ILogger<FinanceController> _logger;
        private readonly NewsService _newsService; // 👈 EKLENDİ

        // NewsService'i controller'a enjekte ediyoruz
        public FinanceController(ILogger<FinanceController> logger, NewsService newsService) // 👈 GÜNCELLENDİ
        {
            _logger = logger;
            _newsService = newsService; // 👈 EKLENDİ
        }

        /// <summary>
        /// Dashboard için gerekli kur ve haber verilerini çeker.
        /// </summary>
        [HttpGet("dashboard-data")]
        [ProducesResponseType(typeof(DashboardDataDto), 200)] // 👈 GÜNCELLENDİ
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetDashboardData()
        {
            // Her iki görevi de (Kur çekme ve Haber çekme) paralel olarak başlatıyoruz
            var currencyTask = GetCurrencyRatesAsync();
            var newsTask = _newsService.GetNewsAsync(); // 👈 RSS servisimiz

            // Her ikisinin de bitmesini bekliyoruz
            await Task.WhenAll(currencyTask, newsTask);

            // Sonuçları birleştirip tek bir DTO'da döndürüyoruz
            var response = new DashboardDataDto(
                await currencyTask,
                await newsTask
            );
            
            return Ok(response);
        }
        
        /// <summary>
        /// Kur verilerini Yahoo Finance'ten çeken yardımcı metot
        /// </summary>
        private async Task<List<CurrencyRateDto>> GetCurrencyRatesAsync()
        {
            var currencyRates = new List<CurrencyRateDto>();
            try
            {
                var tickers = TickerMap.Keys.ToArray();
                var fields = new[] { Field.Symbol, Field.ShortName, Field.RegularMarketPrice, Field.RegularMarketChangePercent };
                
                var quotes = await Yahoo.Symbols(tickers).Fields(fields).QueryAsync();

                foreach (var quote in quotes.Values)
                {
                    // Fiyatı sıfır olmayanları ekle
                    if (quote.RegularMarketPrice != 0)
                    {
                        currencyRates.Add(new CurrencyRateDto(
                            // Haritada varsa güzel ismini, yoksa kısa ismini, o da yoksa sembolü al
                            TickerMap.GetValueOrDefault(quote.Symbol, quote.ShortName ?? quote.Symbol),
                            (double)quote.RegularMarketPrice, // decimal -> double
                            (double)quote.RegularMarketChangePercent // decimal -> double
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yahoo Finance kur verisi çekilemedi.");
                // Hata durumunda bile frontend çökmesin diye boş liste döndür
            }
            return currencyRates;
        }
    }
}