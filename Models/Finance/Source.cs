namespace Cuzdan360Backend.Models.Finance;

public class Source
{
    public int SourceId { get; set; }              // Primary Key
    public string SourceName { get; set; } = null!; // e.g. "Cash", "Bank Account", "Credit Card"

    // Navigation Property (One-to-Many)
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
