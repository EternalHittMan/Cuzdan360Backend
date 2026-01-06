using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using YahooFinanceApi;

namespace Cuzdan360Backend.Services
{
    public class MarketDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MarketDataService> _logger;
        private const string BaseUrl = "https://query1.finance.yahoo.com/v7/finance/quote?symbols=";

        public MarketDataService(HttpClient httpClient, IMemoryCache cache, ILogger<MarketDataService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            
            // User-Agent is required by Yahoo or they block requests
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        public async Task<Dictionary<string, MarketDataDto>> GetCurrentPricesAsync(List<string> symbols)
        {
            if (symbols == null || !symbols.Any())
                return new Dictionary<string, MarketDataDto>();

            var uniqueSymbols = symbols.Distinct().ToList();
            var result = new Dictionary<string, MarketDataDto>();
            var symbolsToFetch = new List<string>();

            // 1. Check Cache
            foreach (var symbol in uniqueSymbols)
            {
                if (_cache.TryGetValue($"Price_{symbol}", out MarketDataDto? cachedData))
                {
                    result[symbol] = cachedData!;
                }
                else
                {
                    symbolsToFetch.Add(symbol);
                }
            }

            if (!symbolsToFetch.Any())
                return result;

            // 2. Fetch from API using YahooFinanceApi Library
            try
            {
                var fields = new[] { Field.Symbol, Field.RegularMarketPrice, Field.RegularMarketChangePercent, Field.Currency };
                var quotes = await Yahoo.Symbols(symbolsToFetch.ToArray()).Fields(fields).QueryAsync();
                
                foreach (var quote in quotes.Values)
                {
                    // Map Library Result to DTO
                    var dto = new MarketDataDto
                    {
                        Symbol = quote.Symbol,
                        Price = (decimal)quote.RegularMarketPrice,
                        ChangePercent = (decimal)quote.RegularMarketChangePercent,
                        Currency = quote.Currency ?? "USD"
                    };

                    // Cache for 5 minutes
                    _cache.Set($"Price_{dto.Symbol}", dto, TimeSpan.FromMinutes(5));
                    result[dto.Symbol] = dto;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching market data via YahooFinanceApi");
            }

            return result;
        }

        public async Task<List<SymbolSearchResultDto>> SearchSymbolsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SymbolSearchResultDto>();

            var searchUrl = $"https://query2.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=10&newsCount=0&enableFuzzyQuery=false&quotesQueryId=tss_match_phrase_query";

            try
            {
                var response = await _httpClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return ParseSearchResponse(content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching symbols");
            }

            return new List<SymbolSearchResultDto>();
        }

        private List<SymbolSearchResultDto> ParseSearchResponse(string json)
        {
            var results = new List<SymbolSearchResultDto>();
            try
            {
                 using var doc = JsonDocument.Parse(json);
                 if (doc.RootElement.TryGetProperty("quotes", out var quotes))
                 {
                     foreach (var quote in quotes.EnumerateArray())
                     {
                         // Filter out irrelevant types (Option, etc if needed)
                         var symbol = quote.TryGetProperty("symbol", out var s) ? s.GetString() : "";
                         var shortname = quote.TryGetProperty("shortname", out var n) ? n.GetString() : 
                                         quote.TryGetProperty("longname", out var ln) ? ln.GetString() : symbol;
                         var type = quote.TryGetProperty("quoteType", out var t) ? t.GetString() : "Unknown";
                         var exchange = quote.TryGetProperty("exchange", out var e) ? e.GetString() : "";

                         if (!string.IsNullOrEmpty(symbol))
                         {
                             results.Add(new SymbolSearchResultDto
                             {
                                 Symbol = symbol,
                                 Name = shortname,
                                 Type = type,
                                 Exchange = exchange
                             });
                         }
                     }
                 }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing search response");
            }
            return results;
        }

        private string FormatSymbol(string symbol)
        {
            return symbol.ToUpper();
        }

        private Dictionary<string, MarketDataDto> ParseYahooResponse(string json)
        {
            var data = new Dictionary<string, MarketDataDto>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("quoteResponse", out var quoteResponse) && 
                    quoteResponse.TryGetProperty("result", out var results))
                {
                    foreach (var quote in results.EnumerateArray())
                    {
                        string symbol = quote.GetProperty("symbol").GetString() ?? "";
                        
                        decimal price = 0;
                        if (quote.TryGetProperty("regularMarketPrice", out var p)) 
                            price = p.GetDecimal();

                        decimal changePercent = 0;
                        if (quote.TryGetProperty("regularMarketChangePercent", out var cp)) 
                            changePercent = cp.GetDecimal();
                            
                        string currency = "USD";
                        if (quote.TryGetProperty("currency", out var c))
                            currency = c.GetString() ?? "USD";

                        if (!string.IsNullOrEmpty(symbol))
                        {
                            data[symbol] = new MarketDataDto 
                            { 
                                Symbol = symbol, 
                                Price = price, 
                                ChangePercent = changePercent,
                                Currency = currency 
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Yahoo response");
            }
            return data;
        }
    }

    public class MarketDataDto
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class SymbolSearchResultDto
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Exchange { get; set; }
    }
}
