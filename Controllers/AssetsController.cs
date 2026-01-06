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

                // 1. Collect symbols for live data with Mapping
                var symbolMap = new Dictionary<string, string>(); // InternalSymbol -> YahooTicker
                
                foreach (var asset in assets)
                {
                    if (string.IsNullOrEmpty(asset.Symbol)) continue;

                    string yahooTicker = asset.Symbol.ToUpper();
                    
                    // Manual Mapping for known types
                    // Assuming asset.Symbol holds values like "USD", "EUR", "BTC" set by the form
                    switch (yahooTicker)
                    {
                        case "USD": yahooTicker = "USDTRY=X"; break;
                        case "EUR": yahooTicker = "EURTRY=X"; break;
                        case "GBP": yahooTicker = "GBPTRY=X"; break;
                        case "XAUTRY": yahooTicker = "XAUTRY=X"; break; // Gram Altın
                        case "BTC": yahooTicker = "BTC-USD"; break; 
                        case "ETH": yahooTicker = "ETH-USD"; break;
                    }
                    
                    symbolMap[asset.Symbol] = yahooTicker;
                }

                var uniqueTickers = symbolMap.Values.Distinct().ToList();

                // 2. Fetch Live Prices
                var marketData = await _marketDataService.GetCurrentPricesAsync(uniqueTickers);

                // 3. Map to DTOs
                var result = assets.Select(asset =>
                {
                    decimal currentPrice = asset.AverageCost; // Default to cost if no price
                    decimal changePercent = 0;
                    string currency = "TRY";

                    // Resolve Yahoo Ticker
                    if (!string.IsNullOrEmpty(asset.Symbol) && symbolMap.TryGetValue(asset.Symbol, out var yahooTicker))
                    {
                        if (marketData.TryGetValue(yahooTicker, out var data))
                        {
                            currentPrice = data.Price;
                            changePercent = data.ChangePercent;
                            currency = data.Currency;
                        }
                    }
                    else if (asset.AssetType != null && asset.AssetType.Code != "TRY") // Existing currency logic backup?
                    {
                        // TODO: Use existing currency cache logic if strict currency asset
                    }

                    // Calculations
                    decimal totalValue = asset.Amount * currentPrice;
                    decimal profitLoss = (currentPrice - asset.AverageCost) * asset.Amount;
                    decimal profitLossPercent = asset.AverageCost != 0 
                        ? ((currentPrice - asset.AverageCost) / asset.AverageCost) * 100 
                        : 0;

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
                        CurrentPrice = currentPrice,
                        TotalValue = totalValue,
                        ProfitLoss = profitLoss,
                        ProfitLossPercent = profitLossPercent,
                        Currency = currency
                    };
                }).ToList();

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
