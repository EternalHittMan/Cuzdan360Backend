using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cuzdan360Backend.Models;

namespace Cuzdan360Backend.Models.Finance
{
    public class RecurringTransaction
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public int SourceId { get; set; }
        [ForeignKey("SourceId")]
        public Source? Source { get; set; }

        public int AssetTypeId { get; set; }
        [ForeignKey("AssetTypeId")]
        public AssetType? AssetType { get; set; }

        // 0 = Income, 1 = Expense
        public int TransactionType { get; set; }

        // 1-31 representing the day of the month this should run
        // 1-31 representing the day of the month this should run
        // OR 0-6 representing DayOfWeek if Frequency is Weekly
        public int DayOfMonth { get; set; }

        public int Frequency { get; set; } // 0=Monthly, 1=Weekly

        public DateTime? LastRunDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
