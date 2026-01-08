using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Models.DTOs;
using Cuzdan360Backend.Services;

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AssetsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger<AssetsController> _logger;

        public AssetsController(AppDbContext context, MarketDataService marketDataService, ILogger<AssetsController> logger)
        {
            _context = context;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Geçersiz kullanıcı kimliği.");
            }
            return userId;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AssetResponseDto>>> GetAssets()
        {
            try
            {
                var userId = GetCurrentUserId();
                var assets = await _context.UserAssets
                    .Include(a => a.AssetType)
                    .Where(a => a.UserId == userId)
                    .ToListAsync();

                // 1. Calculate Cash Balance First
                var allTransactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .Select(t => new { t.TransactionType, t.Amount })
                    .ToListAsync();

                decimal totalIncome = allTransactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount);
                decimal totalExpense = allTransactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount);
                decimal cashBalance = totalIncome - totalExpense;

                // 2. Collect symbols for live data with Unified Mapping
                var tickerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "USD", "USDTRY=X" },
                    { "EUR", "EURTRY=X" },
                    { "GBP", "GBPTRY=X" },
                    { "GA", "XAUTRY=X" },
                    { "XAU", "XAUTRY=X" },
                    { "XAUTRY", "XAUTRY=X" },
                    { "BTC", "BTC-USD" },
                    { "ETH", "ETH-USD" },
                    { "USDT", "USDT-USD" },
                    { "BNB", "BNB-USD" },
                    { "SOL", "SOL-USD" },
                    { "XRP", "XRP-USD" },
                    { "AVAX", "AVAX-USD" }
                };
                
                var querySymbols = new HashSet<string>();

                // Helper to resolve ticker (matches ReportsController)
                string ResolveTicker(string code)
                {
                     if (string.IsNullOrEmpty(code)) return null;
                     if (tickerMap.TryGetValue(code, out var ticker)) return ticker;
                     if (code.Contains(".") || code.EndsWith("=X")) return code;
                     return null;
                }

                foreach (var asset in assets)
                {
                    if (!string.IsNullOrEmpty(asset.Symbol)) querySymbols.Add(asset.Symbol);
                    
                    var ticker = ResolveTicker(asset.AssetType?.Code);
                    if (ticker != null) querySymbols.Add(ticker);
                }

                // Ensure base pairs
                querySymbols.Add("USDTRY=X");
                querySymbols.Add("EURTRY=X");
                querySymbols.Add("XAUTRY=X");

                // 3. Fetch Live Prices
                var livePrices = await _marketDataService.GetCurrentPricesAsync(querySymbols.ToList());

                // Rate Helper (Unified)
                decimal GetRate(string currencyCode)
                {
                     if(string.IsNullOrEmpty(currencyCode) || currencyCode == "TRY") return 1m;
                     
                     string targetKey = ResolveTicker(currencyCode) ?? currencyCode;
                     decimal price = 0;
                     string priceCurrency = "TRY";

                     if(livePrices.TryGetValue(targetKey, out var data))
                     {
                         price = data.Price;
                         priceCurrency = data.Currency;
                     }
                     else
                     {
                         // Fallbacks
                         if(currencyCode == "USD") return 36.0m;
                         if(currencyCode == "EUR") return 38.0m;
                         if(currencyCode == "GA") return 3000m;
                         if(currencyCode == "BTC") price = 95000m;
                         else if(currencyCode == "ETH") price = 2700m;
                         else return 1m;
                         
                         priceCurrency = "USD";
                     }

                     if (priceCurrency == "TRY") return price;
                     if (priceCurrency == "USD") return price * (livePrices.TryGetValue("USDTRY=X", out var usd) ? usd.Price : 36.0m);
                     if (priceCurrency == "EUR") return price * (livePrices.TryGetValue("EURTRY=X", out var eur) ? eur.Price : 38.0m);

                     return price;
                }

                // 4. Map Assets to DTOs
                var result = assets.Select(asset =>
                {
                    decimal price = 1;
                    string code = asset.AssetType?.Code ?? "TRY";
                    string lookupKey = !string.IsNullOrEmpty(asset.Symbol) ? asset.Symbol : asset.AssetType?.Code;
                    string ticker = ResolveTicker(lookupKey) ?? lookupKey;

                    if(!string.IsNullOrEmpty(ticker) && livePrices.ContainsKey(ticker))
                         price = livePrices[ticker].Price;
                    else
                         price = GetRate(code);

                    // Normalize to TRY if the price found was in foreign currency (re-use GetRate logic partly or simplify)
                    // The simplest way to be consistent is just call GetRate on the AssetType Code if no specific Symbol overrides
                    
                    // Improved Logic:
                    decimal exchangeRate = 1;
                    if (!string.IsNullOrEmpty(asset.Symbol) && livePrices.TryGetValue(asset.Symbol, out var symbolData))
                    {
                        // If symbol exists (e.g. AAPL), convert its currency to TRY
                        if (symbolData.Currency == "USD") exchangeRate = (livePrices.TryGetValue("USDTRY=X", out var u) ? u.Price : 36m);
                        else if (symbolData.Currency == "EUR") exchangeRate = (livePrices.TryGetValue("EURTRY=X", out var e) ? e.Price : 38m);
                        price = symbolData.Price * exchangeRate;
                    }
                    else
                    {
                        // Generic Asset
                        price = GetRate(code);
                    }

                    return new AssetResponseDto
                    {
                        Id = asset.Id,
                        UserId = asset.UserId,
                        AssetTypeId = asset.AssetTypeId,
                        AssetTypeName = asset.AssetType?.Name ?? "Bilinmiyor",
                        AssetTypeCode = asset.AssetType?.Code ?? "",
                        Amount = asset.Amount,
                        AverageCost = asset.AverageCost,
                        Symbol = asset.Symbol,
                        AssetCategory = asset.AssetCategory,
                        CurrentPrice = price, // Now in TRY
                        TotalValue = asset.Amount * price,
                        ProfitLoss = (price - asset.AverageCost) * asset.Amount,
                        ProfitLossPercent = asset.AverageCost != 0 ? ((price - asset.AverageCost) / asset.AverageCost) * 100 : 0,
                        Currency = "TRY"
                    };
                }).ToList();

                // 5. Inject Cash Balance as a "Virtual" Asset
                if (cashBalance > 0)
                {
                    result.Insert(0, new AssetResponseDto
                    {
                        Id = 0, // Virtual
                        UserId = userId,
                        AssetTypeId = 0,
                        AssetTypeName = "Nakit Varlıklar",
                        AssetTypeCode = "TRY",
                        Amount = cashBalance,
                        AverageCost = 1,
                        Symbol = "CASH",
                        AssetCategory = "Nakit",
                        CurrentPrice = 1,
                        TotalValue = cashBalance,
                        ProfitLoss = 0,
                        ProfitLossPercent = 0,
                        Currency = "TRY"
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Varlıklar getirilirken hata oluştu.");
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<SymbolSearchResultDto>>> SearchSymbols([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Ok(new List<SymbolSearchResultDto>());
            }
            
            var results = await _marketDataService.SearchSymbolsAsync(q);
            return Ok(results);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();

                // Duplication Check Logic
                // If Symbol is provided, check by Symbol.
                // If not, check by AssetTypeId (for generic assets like GOLD, USD)
                UserAsset? existingAsset = null;

                if (!string.IsNullOrEmpty(request.Symbol))
                {
                    existingAsset = await _context.UserAssets
                        .FirstOrDefaultAsync(u => u.UserId == userId && u.Symbol == request.Symbol);
                }
                else
                {
                    existingAsset = await _context.UserAssets
                        .FirstOrDefaultAsync(u => u.UserId == userId && u.AssetTypeId == request.AssetTypeId && string.IsNullOrEmpty(u.Symbol));
                }

                if (existingAsset != null)
                {
                    return BadRequest(new { error = "Bu varlık zaten portföyünüzde. Lütfen miktar güncelleyin." });
                }

                var newAsset = new UserAsset
                {
                    UserId = userId,
                    AssetTypeId = request.AssetTypeId,
                    Amount = request.Amount,
                    AverageCost = request.AverageCost,
                    Symbol = request.Symbol,
                    AssetCategory = request.AssetCategory
                };

                _context.UserAssets.Add(newAsset);
                await _context.SaveChangesAsync();
                
                // Fetch for consistent return
                // We could just return 'newAsset' but we want the DTO format
                // For simplicity returning OK with ID, client usually refetches or we can return DTO manually
                return Ok(new { id = newAsset.Id, message = "Varlık eklendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Varlık oluşturulurken hata oluştu.");
                return StatusCode(500, new { error = "Varlık eklenemedi." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsset(int id, [FromBody] UpdateAssetRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();
                var asset = await _context.UserAssets.FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId);

                if (asset == null)
                    return NotFound(new { error = "Varlık bulunamadı." });

                asset.Amount = request.Amount;
                asset.AverageCost = request.AverageCost;
                if (!string.IsNullOrEmpty(request.Symbol)) 
                    asset.Symbol = request.Symbol;
                
                await _context.SaveChangesAsync();
                return Ok(asset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Varlık güncellenirken hata oluştu.");
                return StatusCode(500, new { error = "Güncelleme başarısız." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsset(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var asset = await _context.UserAssets.FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId);

                if (asset == null)
                    return NotFound(new { error = "Varlık bulunamadı." });

                _context.UserAssets.Remove(asset);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Varlık silinirken hata oluştu.");
                return StatusCode(500, new { error = "Silme işlemi başarısız." });
            }
        }
    }
}
