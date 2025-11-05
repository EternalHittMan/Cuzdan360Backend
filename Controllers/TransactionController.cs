// Dosya: Controllers/TransactionsController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cuzdan360Backend.Repositories;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Models.DTOs;
using System.Security.Claims;
using Cuzdan360Backend.Data; // 👈 1. EKLENMELİ (DbContext için)
using Microsoft.EntityFrameworkCore; // 👈 2. EKLENMELİ (ToListAsync için)

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionRepository _transactionRepo;
        private readonly AppDbContext _context; // 👈 3. EKLENMELİ (Lookup verileri için)

        // 4. CONSTRUCTOR GÜNCELLENMELİ: AppDbContext eklenmeli
        public TransactionsController(ITransactionRepository transactionRepo, AppDbContext context)
        {
            _transactionRepo = transactionRepo;
            _context = context; 
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

            // 🔽 === 5. DÜZELTME (Ekleme sonrası 'Invalid Date' sorunu için) === 🔽
            // Frontend'in tabloyu güncelleyebilmesi için,
            // ilişkili verileri (Category, Source vb.) içeren tam objeyi geri dönmeliyiz.
            var newTransactionWithIncludes = await _transactionRepo.GetTransactionByIdAsync(transaction.TransactionId, userId);
            // 🔼 === DÜZELTME SONU === 🔼

            // 6. DÖNÜŞ DEĞERİ GÜNCELLENDİ
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, newTransactionWithIncludes);
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

            return NoContent(); 
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

            return NoContent(); 
        }

        
        // === 7. YENİ ENDPOINT'LER ("Veri Yükleme Hatası" sorunu için) ===

        /// <summary>
        /// Formda kullanılacak tüm kategorileri listeler.
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Select(c => new { c.CategoryId, c.Name })
                .OrderBy(c => c.Name) // Alfabetik sırala
                .ToListAsync();
            return Ok(categories);
        }

        /// <summary>
        /// Formda kullanılacak tüm kaynakları listeler.
        /// </summary>
        [HttpGet("sources")]
        public async Task<IActionResult> GetSources()
        {
            var sources = await _context.Sources
                .Select(s => new { s.SourceId, s.SourceName })
                .OrderBy(s => s.SourceName)
                .ToListAsync();
            return Ok(sources);
        }

        /// <summary>
        /// Formda kullanılacak tüm varlık tiplerini listeler.
        /// </summary>
        [HttpGet("asset-types")]
        public async Task<IActionResult> GetAssetTypes()
        {
            var assetTypes = await _context.AssetTypes
                .Select(a => new { a.AssetTypeId, a.Name, a.Code })
                .OrderBy(a => a.Name)
                .ToListAsync();
            return Ok(assetTypes);
        }
        
        // === YENİ ENDPOINT'LER SONU ===


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