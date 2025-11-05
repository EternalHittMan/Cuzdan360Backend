namespace Cuzdan360Backend.Models.Finance;

public class AssetType
{
    public int AssetTypeId { get; set; }          // ✅ Doğru Primary Key
    public string Name { get; set; } = null!;     // e.g. "TL", "USD", "Altın"
    public string Code { get; set; } = null!;     // e.g. "TRY", "USD"

    // Navigation Property (One-to-Many)
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
