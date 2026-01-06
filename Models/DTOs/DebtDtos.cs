using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs
{
    public class CreateDebtRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır.")]
        public decimal Amount { get; set; }

        [Required]
        public int AssetTypeId { get; set; }

        public DateTime? DueDate { get; set; }
        public string? LenderName { get; set; }

        public string CurrencySymbol { get; set; } = "TRY";
        public decimal InitialAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int TotalInstallments { get; set; } = 1;
        public int RemainingInstallments { get; set; } = 1;
    }

    public class UpdateDebtRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır.")]
        public decimal Amount { get; set; }
        
        public DateTime? DueDate { get; set; }
        public string? LenderName { get; set; }

        public decimal InterestRate { get; set; }
        public int TotalInstallments { get; set; }
        public int RemainingInstallments { get; set; }
    }
}
