using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs
{
    public class CreateAssetRequest
    {
        [Required]
        public int AssetTypeId { get; set; }

        [Required]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır.")]
        public decimal Amount { get; set; }

        public decimal AverageCost { get; set; }
        public string? Symbol { get; set; }
        public string AssetCategory { get; set; } = "Other";
    }

    public class UpdateAssetRequest
    {
        [Required]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır.")]
        public decimal Amount { get; set; }

        public decimal AverageCost { get; set; }
        public string? Symbol { get; set; }
    }

    public class AssetResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AssetTypeId { get; set; }
        public string AssetTypeName { get; set; }
        public string AssetTypeCode { get; set; }
        public decimal Amount { get; set; }
        public decimal AverageCost { get; set; }
        public string? Symbol { get; set; }
        public string AssetCategory { get; set; }
        
        // Calculated/Live Data
        public decimal CurrentPrice { get; set; }
        public decimal TotalValue { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal ProfitLossPercent { get; set; }
        public string Currency { get; set; } = "TRY";
    }
}
