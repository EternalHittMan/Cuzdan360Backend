// Dosya: Controllers/TransactionsController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cuzdan360Backend.Repositories;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Models.DTOs;
using System.Security.Claims;

namespace Cuzdan360Backend.Controllers
{
    [Authorize] // 👈 Sadece giriş yapmış kullanıcılar erişebilir
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionRepository _transactionRepo;

        public TransactionsController(ITransactionRepository transactionRepo)
        {
            _transactionRepo = transactionRepo;
        }

        /// <summary>
        /// O an giriş yapmış kullanıcının tüm işlemlerini listeler.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserTransactions()
        {
            var userId = GetCurrentUserId();
            var transactions = await _transactionRepo.GetTransactionsByUserIdAsync(userId);
            return Ok(transactions);
        }

        /// <summary>
        /// Tek bir işlemi ID'ye göre getirir.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransaction(int id)
        {
            var userId = GetCurrentUserId();
            var transaction = await _transactionRepo.GetTransactionByIdAsync(id, userId);

            if (transaction == null)
            {
                // Kullanıcı ya başkasının işlemine ya da var olmayan bir işleme erişmeye çalıştı
                return NotFound(new { error = "İşlem bulunamadı." });
            }

            return Ok(transaction);
        }

        /// <summary>
        /// Yeni bir işlem (Gelir/Gider) oluşturur.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
        {
            var userId = GetCurrentUserId();

            // DTO'yu ana Transaction modeline dönüştür
            var transaction = new Transaction
            {
                UserId = userId,
                AssetTypeId = request.AssetTypeId,
                CategoryId = request.CategoryId,
                SourceId = request.SourceId,
                TransactionType = request.TransactionType,
                Amount = request.Amount,
                Title = request.Title,
                TransactionDate = request.TransactionDate.ToUniversalTime()
            };

            await _transactionRepo.AddTransactionAsync(transaction);

            // Başarılı oluşturma için 201 Created yanıtı ve verinin konumu
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
        }

        /// <summary>
        /// Mevcut bir işlemi günceller.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTransaction(int id, [FromBody] CreateTransactionRequest request)
        {
            var userId = GetCurrentUserId();
            var transaction = await _transactionRepo.GetTransactionByIdAsync(id, userId);

            if (transaction == null)
            {
                return NotFound(new { error = "Güncellenecek işlem bulunamadı." });
            }

            // Modeli güncelle
            transaction.AssetTypeId = request.AssetTypeId;
            transaction.CategoryId = request.CategoryId;
            transaction.SourceId = request.SourceId;
            transaction.TransactionType = request.TransactionType;
            transaction.Amount = request.Amount;
            transaction.Title = request.Title;
            transaction.TransactionDate = request.TransactionDate.ToUniversalTime();

            await _transactionRepo.UpdateTransactionAsync(transaction);

            return NoContent(); // 204 No Content - Başarılı güncelleme
        }

        /// <summary>
        /// Mevcut bir işlemi siler.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var userId = GetCurrentUserId();
            var transaction = await _transactionRepo.GetTransactionByIdAsync(id, userId);

            if (transaction == null)
            {
                return NotFound(new { error = "Silinecek işlem bulunamadı." });
            }

            await _transactionRepo.DeleteTransactionAsync(transaction);

            return NoContent(); // 204 No Content - Başarılı silme
        }


        /// <summary>
        /// JWT tokendan o anki kullanıcının ID'sini çeken yardımcı metot.
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Geçersiz token. Kullanıcı kimliği bulunamadı.");
            }
            return int.Parse(userIdClaim);
        }
    }
}