using Cuzdan360Backend.Models;  // User class'ına erişmek için
namespace Cuzdan360Backend.Models.Finance;

public class Transaction
{
    public int TransactionId { get; set; }        // Primary Key

    // Foreign Keys
    public int UserId { get; set; }               // Kullanıcı (User tablosuna bağlı)
    public int AssetTypeId { get; set; }          // Para birimi
    public int CategoryId { get; set; }           // Kategori
    public int SourceId { get; set; }             // Kaynak (Cash, Bank, vs.)

    // Fields
    public TransactionType TransactionType { get; set; }  // Income veya Expense
    public decimal Amount { get; set; }                   // İşlem tutarı (örneğin 500.75)
    public string? Title { get; set; }                    // İşlem açıklaması
    public DateTime TransactionDate { get; set; }         // Tarih

    // Navigation Properties
    public User User { get; set; } = null!;
    public AssetType AssetType { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Source Source { get; set; } = null!;
}
