using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cuzdan360Backend.Models;

namespace Cuzdan360Backend.Models.Finance
{
    public class UserDebt
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        public string Title { get; set; } = string.Empty; // e.g. "Kredi Kartı", "Ahmet'e Borç"

        // The amount owed
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public int AssetTypeId { get; set; }
        [ForeignKey("AssetTypeId")]
        public AssetType? AssetType { get; set; }

        public DateTime? DueDate { get; set; } // Ödeme tarihi
        
        public string? LenderName { get; set; } // Alacaklı (Opsiyonel, Title ile aynı olabilir)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(10)]
        public string? CurrencySymbol { get; set; } = "TRY"; // USD, EUR, TRY

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal InterestRate { get; set; } // Yıllık/Aylık faiz oranı

        public int TotalInstallments { get; set; } = 1;
        public int RemainingInstallments { get; set; } = 1;
    }
}
