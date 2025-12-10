using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Models.DTOs;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RecurringTransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RecurringTransactionsController> _logger;

        public RecurringTransactionsController(AppDbContext context, ILogger<RecurringTransactionsController> logger)
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
        public async Task<IActionResult> GetRecurringTransactions()
        {
            try
            {
                var userId = GetCurrentUserId();
                var transactions = await _context.RecurringTransactions
                    .Include(t => t.Category)
                    .Include(t => t.Source)
                    .Include(t => t.AssetType)
                    .Where(t => t.UserId == userId)
                    .ToListAsync();

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tekrarlayan işlemler getirilirken hata");
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRecurringTransactionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = GetCurrentUserId();

                var recurring = new RecurringTransaction
                {
                    UserId = userId,
                    Title = request.Title,
                    Amount = request.Amount,
                    CategoryId = request.CategoryId ?? 0,
                    SourceId = request.SourceId ?? 0,
                    AssetTypeId = request.AssetTypeId ?? 0,
                    TransactionType = request.TransactionType,
                    DayOfMonth = request.DayOfMonth,
                    IsActive = true
                };

                _context.RecurringTransactions.Add(recurring);
                await _context.SaveChangesAsync();

                return Ok(recurring);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tekrarlayan işlem oluşturulurken hata");
                return StatusCode(500, new { error = "Kayıt oluşturulamadı." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateRecurringTransactionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var existing = await _context.RecurringTransactions
                    .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

                if (existing == null)
                    return NotFound(new { error = "Kayıt bulunamadı." });

                existing.Title = request.Title;
                existing.Amount = request.Amount;
                existing.CategoryId = request.CategoryId ?? existing.CategoryId;
                existing.SourceId = request.SourceId ?? existing.SourceId;
                existing.AssetTypeId = request.AssetTypeId ?? existing.AssetTypeId;
                existing.TransactionType = request.TransactionType;
                existing.DayOfMonth = request.DayOfMonth;

                // IMPORTANT: We do not touch LastRunDate. 
                // Updating the rule affects *future* runs only.

                await _context.SaveChangesAsync();
                return Ok(existing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tekrarlayan işlem güncellenirken hata");
                return StatusCode(500, new { error = "Güncelleme yapılamadı." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var existing = await _context.RecurringTransactions
                    .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

                if (existing == null)
                    return NotFound(new { error = "Kayıt bulunamadı." });

                // Soft delete or Hard delete? 
                // User requirement: "When a user deletes... it must ONLY affect future transactions."
                // Hard delete is fine for the rule itself, as long as we don't cascade delete the generated Transactions.
                // Since there is no FK from Transaction to RecurringTransaction (in the current design logic I proposed), 
                // hard delete is safe for history preservation.
                
                _context.RecurringTransactions.Remove(existing);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tekrarlayan işlem silinirken hata");
                return StatusCode(500, new { error = "Silme işlemi yapılamadı." });
            }
        }
    }
}
