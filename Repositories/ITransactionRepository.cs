using Cuzdan360Backend.Models.Finance;

namespace Cuzdan360Backend.Repositories
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Bir kullanıcıya ait tüm işlemleri tarihe göre sıralı getirir.
        /// </summary>
        Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(int userId);

        /// <summary>
        /// Bir kullanıcıya ait tek bir işlemi getirir (güvenlik için userId ile).
        /// </summary>
        Task<Transaction?> GetTransactionByIdAsync(int transactionId, int userId);

        /// <summary>
        /// Yeni bir işlem ekler.
        /// </summary>
        Task AddTransactionAsync(Transaction transaction);

        /// <summary>
        /// Mevcut bir işlemi günceller.
        /// </summary>
        Task UpdateTransactionAsync(Transaction transaction);

        /// <summary>
        /// Mevcut bir işlemi siler.
        /// </summary>
        Task DeleteTransactionAsync(Transaction transaction);

        /// <summary>
        /// Toplu işlem ekler.
        /// </summary>
        Task AddRangeAsync(IEnumerable<Transaction> transactions);
    }
}