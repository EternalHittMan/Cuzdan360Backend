// Dosya: Repositories/TransactionRepository.cs

using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.Finance;
using Microsoft.EntityFrameworkCore;

namespace Cuzdan360Backend.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly AppDbContext _context;

        public TransactionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Transaction?> GetTransactionByIdAsync(int transactionId, int userId)
        {
            // İşlemi getirirken hem ID'yi hem de kullanıcı ID'sini kontrol et
            return await _context.Transactions
                .Include(t => t.Category)
                .Include(t => t.Source)
                .Include(t => t.AssetType)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(int userId)
        {
            // Kullanıcının tüm işlemlerini getir, ilgili verileri (Kategori vb.) .Include() ile dahil et
            return await _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Category)
                .Include(t => t.Source)
                .Include(t => t.AssetType)
                .OrderByDescending(t => t.TransactionDate) // En yeniden eskiye sırala
                .ToListAsync();
        }

        public async Task AddTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();
        }
    }
}