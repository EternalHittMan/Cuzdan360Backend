namespace Cuzdan360Backend.Models.Finance;

public class Category
{
    public int CategoryId { get; set; }            // Primary Key
    public string Name { get; set; } = null!;      // e.g. "Food", "Salary", "Transport"

    // Navigation Property (One-to-Many)
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
