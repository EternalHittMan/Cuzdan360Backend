using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cuzdan360Backend.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TransactionRepository> _logger;

        public TransactionRepository(AppDbContext context, ILogger<TransactionRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Transaction?> GetTransactionByIdAsync(int transactionId, int userId)
        {
            try
            {
                _logger.LogDebug("GetTransactionByIdAsync çağrıldı. TransactionId: {TransactionId}, UserId: {UserId}", 
                    transactionId, userId);

                var transaction = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.Source)
                    .Include(t => t.AssetType)
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);

                if (transaction == null)
                {
                    _logger.LogWarning("İşlem bulunamadı. TransactionId: {TransactionId}, UserId: {UserId}", 
                        transactionId, userId);
                }

                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTransactionByIdAsync'de hata. TransactionId: {TransactionId}, UserId: {UserId}", 
                    transactionId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(int userId)
        {
            try
            {
                _logger.LogDebug("GetTransactionsByUserIdAsync çağrıldı. UserId: {UserId}", userId);

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .Include(t => t.Category)
                    .Include(t => t.Source)
                    .Include(t => t.AssetType)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                _logger.LogDebug("Toplam {Count} işlem bulundu. UserId: {UserId}", transactions.Count, userId);

                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTransactionsByUserIdAsync'de hata. UserId: {UserId}", userId);
                throw;
            }
        }

        public async Task AddTransactionAsync(Transaction transaction)
        {
            try
            {
                _logger.LogDebug("AddTransactionAsync çağrıldı. UserId: {UserId}, Amount: {Amount}", 
                    transaction.UserId, transaction.Amount);

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation("İşlem başarıyla eklendi. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "AddTransactionAsync'de veritabanı hatası. UserId: {UserId}, " +
                    "AssetTypeId: {AssetTypeId}, CategoryId: {CategoryId}, SourceId: {SourceId}", 
                    transaction.UserId, transaction.AssetTypeId, transaction.CategoryId, transaction.SourceId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddTransactionAsync'de beklenmeyen hata. UserId: {UserId}", 
                    transaction.UserId);
                throw;
            }
        }

        public async Task UpdateTransactionAsync(Transaction transaction)
        {
            try
            {
                _logger.LogDebug("UpdateTransactionAsync çağrıldı. TransactionId: {TransactionId}", 
                    transaction.TransactionId);

                _context.Transactions.Update(transaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation("İşlem başarıyla güncellendi. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "UpdateTransactionAsync'de veritabanı hatası. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateTransactionAsync'de beklenmeyen hata. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
                throw;
            }
        }

        public async Task DeleteTransactionAsync(Transaction transaction)
        {
            try
            {
                _logger.LogDebug("DeleteTransactionAsync çağrıldı. TransactionId: {TransactionId}", 
                    transaction.TransactionId);

                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation("İşlem başarıyla silindi. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DeleteTransactionAsync'de veritabanı hatası. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteTransactionAsync'de beklenmeyen hata. TransactionId: {TransactionId}", 
                    transaction.TransactionId);
                throw;
            }
        }

        public async Task AddRangeAsync(IEnumerable<Transaction> transactions)
        {
            try
            {
                _logger.LogDebug("AddRangeAsync çağrıldı. İşlem sayısı: {Count}", transactions.Count());

                await _context.Transactions.AddRangeAsync(transactions);
                await _context.SaveChangesAsync();

                _logger.LogInformation("{Count} adet işlem başarıyla eklendi.", transactions.Count());
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "AddRangeAsync'de veritabanı hatası.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddRangeAsync'de beklenmeyen hata.");
                throw;
            }
        }
    }
}