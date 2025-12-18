using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cuzdan360Backend.Repositories;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Models.DTOs;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Microsoft.EntityFrameworkCore;
using Cuzdan360Backend.Services;
using ClosedXML.Excel;
using System.IO;


namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionRepository _transactionRepo;
        private readonly AppDbContext _context;
        private readonly ILogger<TransactionController> _logger;
        private readonly GeminiReceiptService _geminiService;

        public TransactionController(
            ITransactionRepository transactionRepo, 
            AppDbContext context,
            ILogger<TransactionController> logger,
            GeminiReceiptService geminiService)
        {
            _transactionRepo = transactionRepo;
            _context = context;
            _logger = logger;
            _geminiService = geminiService;
        }

        /// <summary>
        /// O an giriş yapmış kullanıcının tüm işlemlerini listeler.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserTransactions()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("İşlemler getiriliyor. UserId: {UserId}", userId);
                
                var transactions = await _transactionRepo.GetTransactionsByUserIdAsync(userId);
                
                _logger.LogInformation("Toplam {Count} işlem bulundu", transactions.Count());
                
                return Ok(transactions);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Yetkisiz erişim denemesi");
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşlemler getirilirken hata oluştu");
                return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
            }
        }

        /// <summary>
        /// Tek bir işlemi ID'ye göre getirir.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransaction(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var transaction = await _transactionRepo.GetTransactionByIdAsync(id, userId);

                if (transaction == null)
                {
                    return NotFound(new { error = "İşlem bulunamadı." });
                }

                return Ok(transaction);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşlem getirilirken hata oluştu. TransactionId: {TransactionId}", id);
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        /// <summary>
        /// Yeni bir işlem (Gelir/Gider) oluşturur.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(new { error = string.Join(", ", errors) });
                }

                var userId = GetCurrentUserId();
                
                _logger.LogInformation("Yeni işlem oluşturuluyor. UserId: {UserId}, Type: {Type}, Amount: {Amount}", 
                    userId, request.TransactionType, request.Amount);

                var transaction = new Transaction
                {
                    UserId = userId,
                    AssetTypeId = request.AssetTypeId.Value,
                    CategoryId = request.CategoryId.Value,
                    SourceId = request.SourceId.Value,
                    TransactionType = request.TransactionType,
                    Amount = request.Amount,
                    Title = request.Title,
                    TransactionDate = DateTime.SpecifyKind(request.TransactionDate, DateTimeKind.Utc)
                };

                await _transactionRepo.AddTransactionAsync(transaction);

                // Frontend için tam veri dön
                var newTransactionWithIncludes = await _transactionRepo.GetTransactionByIdAsync(
                    transaction.TransactionId, 
                    userId);

                _logger.LogInformation("İşlem başarıyla oluşturuldu. TransactionId: {TransactionId}", 
                    transaction.TransactionId);

                return CreatedAtAction(
                    nameof(GetTransaction), 
                    new { id = transaction.TransactionId }, 
                    newTransactionWithIncludes);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Veritabanı hatası - İşlem oluşturulamadı");
                return StatusCode(500, new { error = "İşlem kaydedilirken bir hata oluştu. Lütfen girdiğiniz verileri kontrol edin." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşlem oluşturulurken beklenmeyen hata");
                return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
            }
        }

        /// <summary>
        /// Mevcut bir işlemi günceller.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTransaction(int id, [FromBody] CreateTransactionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(new { error = string.Join(", ", errors) });
                }

                var userId = GetCurrentUserId();
                var transaction = await _transactionRepo.GetTransactionByIdAsync(id, userId);

                if (transaction == null)
                {
                    return NotFound(new { error = "Güncellenecek işlem bulunamadı." });
                }

                _logger.LogInformation("İşlem güncelleniyor. TransactionId: {TransactionId}", id);

                // Modeli güncelle
                transaction.AssetTypeId = request.AssetTypeId.Value;
                transaction.CategoryId = request.CategoryId.Value;
                transaction.SourceId = request.SourceId.Value;
                transaction.TransactionType = request.TransactionType;
                transaction.Amount = request.Amount;
                transaction.Title = request.Title;
                transaction.TransactionDate = DateTime.SpecifyKind(request.TransactionDate, DateTimeKind.Utc);

                await _transactionRepo.UpdateTransactionAsync(transaction);

                _logger.LogInformation("İşlem başarıyla güncellendi. TransactionId: {TransactionId}", id);

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Veritabanı hatası - İşlem güncellenemedi. TransactionId: {TransactionId}", id);
                return StatusCode(500, new { error = "İşlem güncellenirken bir hata oluştu." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşlem güncellenirken hata. TransactionId: {TransactionId}", id);
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        /// <summary>
        /// Mevcut bir işlemi siler.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var transaction = await _transactionRepo.GetTransactionByIdAsync(id, userId);

                if (transaction == null)
                {
                    return NotFound(new { error = "Silinecek işlem bulunamadı." });
                }

                _logger.LogInformation("İşlem siliniyor. TransactionId: {TransactionId}", id);

                await _transactionRepo.DeleteTransactionAsync(transaction);

                _logger.LogInformation("İşlem başarıyla silindi. TransactionId: {TransactionId}", id);

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşlem silinirken hata. TransactionId: {TransactionId}", id);
                return StatusCode(500, new { error = "Bir hata oluştu." });
            }
        }

        /// <summary>
        /// Formda kullanılacak tüm kategorileri listeler.
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Select(c => new { c.CategoryId, c.Name })
                    .OrderBy(c => c.Name)
                    .ToListAsync();
                
                _logger.LogInformation("Kategoriler başarıyla getirildi. Toplam: {Count}", categories.Count);
                
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategoriler getirilirken hata");
                return StatusCode(500, new { error = "Kategoriler yüklenemedi." });
            }
        }

        /// <summary>
        /// Formda kullanılacak tüm kaynakları listeler.
        /// </summary>
        [HttpGet("sources")]
        public async Task<IActionResult> GetSources()
        {
            try
            {
                var sources = await _context.Sources
                    .Select(s => new { s.SourceId, s.SourceName })
                    .OrderBy(s => s.SourceName)
                    .ToListAsync();
                
                _logger.LogInformation("Kaynaklar başarıyla getirildi. Toplam: {Count}", sources.Count);
                
                return Ok(sources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kaynaklar getirilirken hata");
                return StatusCode(500, new { error = "Kaynaklar yüklenemedi." });
            }
        }

        /// <summary>
        /// Formda kullanılacak tüm varlık tiplerini listeler.
        /// </summary>
        [HttpGet("asset-types")]
        public async Task<IActionResult> GetAssetTypes()
        {
            try
            {
                var assetTypes = await _context.AssetTypes
                    .Select(a => new { a.AssetTypeId, a.Name, a.Code })
                    .OrderBy(a => a.Name)
                    .ToListAsync();
                
                _logger.LogInformation("Varlık tipleri başarıyla getirildi. Toplam: {Count}", assetTypes.Count);
                
                return Ok(assetTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Varlık tipleri getirilirken hata");
                return StatusCode(500, new { error = "Varlık tipleri yüklenemedi." });
            }
        }

        /// <summary>
        /// JWT tokendan o anki kullanıcının ID'sini çeken yardımcı metot.
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Token'da kullanıcı ID'si bulunamadı");
                throw new UnauthorizedAccessException("Geçersiz token. Kullanıcı kimliği bulunamadı.");
            }
            
            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Token'daki kullanıcı ID'si parse edilemedi: {UserIdClaim}", userIdClaim);
                throw new UnauthorizedAccessException("Geçersiz kullanıcı kimliği.");
            }
            
            return userId;
        }

        /// <summary>
        /// Fiş/Fatura görselini analiz eder ve işlem önerileri döner.
        /// </summary>
        [HttpPost("analyze-receipt")]
        public async Task<IActionResult> AnalyzeReceipt(IFormFile file)
        {
            try
            {
                _logger.LogInformation("Fiş analizi başlatıldı.");
                
                // Context verilerini çek
                var categories = await _context.Categories.ToListAsync();
                var sources = await _context.Sources.ToListAsync();
                var assetTypes = await _context.AssetTypes.ToListAsync();

                var extractedTransactions = await _geminiService.AnalyzeReceiptAsync(file, categories, sources, assetTypes);

                // Smart Matching (Artık Gemini ID dönüyor ama yine de string match fallback kalabilir veya kaldırılabilir. 
                // Gemini ID döndüğü için buradaki döngüye gerek kalmayabilir ama DTO'da eksik gelirse diye string match'i koruyalım mı?
                // Kullanıcı "promptta düzenleme ekle" dedi, yani ID'lerin prompttan gelmesini istiyor.
                // Kod temizliği için manuel eşleştirmeyi kaldırıyorum çünkü prompt artık bunu yapıyor.)

                return Ok(extractedTransactions);


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fiş analizi sırasında hata.");
                return StatusCode(500, new { error = "Fiş analizi başarısız oldu." });
            }
        }

        /// <summary>
        /// Toplu işlem ekler.
        /// </summary>
        [HttpPost("bulk-create")]
        public async Task<IActionResult> BulkCreateTransactions([FromBody] BulkCreateTransactionRequest request)
        {
            try
            {
                if (request?.Transactions == null || !request.Transactions.Any())
                {
                    return BadRequest(new { error = "Eklenecek işlem bulunamadı." });
                }

                var userId = GetCurrentUserId();
                var transactions = new List<Transaction>();

                foreach (var item in request.Transactions)
                {
                    transactions.Add(new Transaction
                    {
                        UserId = userId,
                        Title = item.Title,
                        Amount = item.Amount,
                        TransactionDate = DateTime.SpecifyKind(item.TransactionDate, DateTimeKind.Utc),
                        TransactionType = item.TransactionType,
                        CategoryId = item.CategoryId.Value,
                        SourceId = item.SourceId.Value,
                        AssetTypeId = item.AssetTypeId.Value
                    });
                }

                await _transactionRepo.AddRangeAsync(transactions);

                return Ok(new { message = $"{transactions.Count} işlem başarıyla eklendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu işlem ekleme hatası.");
                return StatusCode(500, new { error = "Toplu işlem eklenirken bir hata oluştu." });
            }
        }
        /// <summary>
        /// Excel dosyasından işlem yükler.
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> ImportTransactions(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "Dosya yüklenmedi." });

                var transactions = new List<Transaction>();
                var userId = GetCurrentUserId();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Header'ı atla

                        foreach (var row in rows)
                        {
                            try
                            {
                                // Örnek Format: Date | Description | Amount | Type (Income/Expense)
                                var dateVal = row.Cell(1).GetDateTime();
                                var descVal = row.Cell(2).GetValue<string>();
                                var amountVal = row.Cell(3).GetValue<decimal>();
                                var typeVal = row.Cell(4).GetValue<string>(); // "Gelir" veya "Gider"

                                int type = (typeVal?.ToLower().Contains("gelir") == true) ? 0 : 1;

                                transactions.Add(new Transaction
                                {
                                    UserId = userId,
                                    TransactionDate = DateTime.SpecifyKind(dateVal, DateTimeKind.Utc),
                                    Title = descVal,
                                    Amount = amountVal,
                                    TransactionType = (TransactionType)type,
                                    CategoryId = 22, // Varsayılan: Diğer Giderler (veya mantıklı bir default)
                                    SourceId = 1,    // Varsayılan: Nakit
                                    AssetTypeId = 1  // Varsayılan: TRY
                                });
                            }
                            catch
                            {
                                // Satır hatası varsa atla veya logla
                                continue;
                            }
                        }
                    }
                }

                if (transactions.Any())
                {
                    await _transactionRepo.AddRangeAsync(transactions);
                    return Ok(new { message = $"{transactions.Count} işlem başarıyla yüklendi." });
                }

                return BadRequest(new { error = "Hiçbir işlem yüklenemedi. Formatı kontrol edin." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel yükleme hatası.");
                return StatusCode(500, new { error = "Dosya işlenirken hata oluştu." });
            }
        }

        /// <summary>
        /// Tüm işlemleri Excel olarak indirir.
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportTransactions()
        {
            try
            {
                var userId = GetCurrentUserId();
                var transactions = await _transactionRepo.GetTransactionsByUserIdAsync(userId);
                
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("İşlemler");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "Tarih";
                    worksheet.Cell(1, 2).Value = "Başlık";
                    worksheet.Cell(1, 3).Value = "Tutar";
                    worksheet.Cell(1, 4).Value = "Tip";
                    worksheet.Cell(1, 5).Value = "Kategori";
                    worksheet.Cell(1, 6).Value = "Kaynak";

                    int row = 2;
                    foreach (var t in transactions)
                    {
                        worksheet.Cell(row, 1).Value = t.TransactionDate;
                        worksheet.Cell(row, 2).Value = t.Title;
                        worksheet.Cell(row, 3).Value = t.Amount;
                        worksheet.Cell(row, 4).Value = t.TransactionType == 0 ? "Gelir" : "Gider";
                        worksheet.Cell(row, 5).Value = t.Category?.Name ?? "-";
                        worksheet.Cell(row, 6).Value = t.Source?.SourceName ?? "-";
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Islemler_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel dışa aktarma hatası.");
                return StatusCode(500, new { error = "Dosya oluşturulamadı." });
            }
        }
    }
}
