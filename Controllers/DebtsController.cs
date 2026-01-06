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
    public class DebtsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DebtsController> _logger;

        public DebtsController(AppDbContext context, ILogger<DebtsController> logger)
        {
            _context = context;
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
        public async Task<IActionResult> GetDebts()
        {
            try
            {
                var userId = GetCurrentUserId();
                var debts = await _context.UserDebts
                    .Include(d => d.AssetType)
                    .Where(d => d.UserId == userId)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
                return Ok(debts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Borçlar getirilirken hata oluştu.");
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDebt([FromBody] CreateDebtRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();

                var newDebt = new UserDebt
                {
                    UserId = userId,
                    Title = request.Title,
                    Amount = request.Amount,
                    AssetTypeId = request.AssetTypeId,
                    DueDate = request.DueDate,
                    LenderName = request.LenderName,
                    CurrencySymbol = request.CurrencySymbol,
                    InitialAmount = request.InitialAmount > 0 ? request.InitialAmount : request.Amount,
                    InterestRate = request.InterestRate,
                    TotalInstallments = request.TotalInstallments,
                    RemainingInstallments = request.RemainingInstallments > 0 ? request.RemainingInstallments : request.TotalInstallments
                };

                _context.UserDebts.Add(newDebt);
                await _context.SaveChangesAsync();
                
                var debtWithDetails = await _context.UserDebts
                    .Include(d => d.AssetType)
                    .FirstOrDefaultAsync(d => d.Id == newDebt.Id);

                return CreatedAtAction(nameof(GetDebts), new { id = newDebt.Id }, debtWithDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Borç oluşturulurken hata oluştu.");
                return StatusCode(500, new { error = "Borç eklenemedi." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDebt(int id, [FromBody] UpdateDebtRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();
                var debt = await _context.UserDebts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

                if (debt == null)
                    return NotFound(new { error = "Borç bulunamadı." });

                debt.Title = request.Title;
                debt.Amount = request.Amount;
                debt.DueDate = request.DueDate;
                debt.LenderName = request.LenderName;
                debt.InterestRate = request.InterestRate;
                debt.TotalInstallments = request.TotalInstallments;
                debt.RemainingInstallments = request.RemainingInstallments;
                
                await _context.SaveChangesAsync();
                return Ok(debt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Borç güncellenirken hata oluştu.");
                return StatusCode(500, new { error = "Güncelleme başarısız." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDebt(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var debt = await _context.UserDebts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

                if (debt == null)
                    return NotFound(new { error = "Borç bulunamadı." });

                _context.UserDebts.Remove(debt);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Borç silinirken hata oluştu.");
                return StatusCode(500, new { error = "Silme işlemi başarısız." });
            }
        }
    }
}
