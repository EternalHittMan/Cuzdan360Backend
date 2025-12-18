using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Models.DTOs;

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AssetsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AssetsController> _logger;

        public AssetsController(AppDbContext context, ILogger<AssetsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Geçersiz token. Kullanıcı kimliği bulunamadı.");
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Geçersiz kullanıcı kimliği.");
            }
            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetAssets()
        {
            try
            {
                var userId = GetCurrentUserId();
                var assets = await _context.UserAssets
                    .Include(a => a.AssetType)
                    .Where(a => a.UserId == userId)
                    .ToListAsync();
                return Ok(assets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Varlıklar getirilirken hata oluştu.");
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();

                // Check if user already has this asset type
                var existingAsset = await _context.UserAssets
                    .FirstOrDefaultAsync(u => u.UserId == userId && u.AssetTypeId == request.AssetTypeId);

                if (existingAsset != null)
                {
                    return BadRequest(new { error = "Bu varlık tipi zaten mevcut. Lütfen güncelleme işlemi yapın." });
                }

                var newAsset = new UserAsset
                {
                    UserId = userId,
                    AssetTypeId = request.AssetTypeId,
                    Amount = request.Amount
                };

                _context.UserAssets.Add(newAsset);
                await _context.SaveChangesAsync();
                
                // Return with Included data
                var assetWithDetails = await _context.UserAssets
                    .Include(a => a.AssetType)
                    .FirstOrDefaultAsync(a => a.Id == newAsset.Id);

                return CreatedAtAction(nameof(GetAssets), new { id = newAsset.Id }, assetWithDetails);
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
