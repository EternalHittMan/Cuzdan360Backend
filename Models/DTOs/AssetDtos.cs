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
    }

    public class UpdateAssetRequest
    {
        [Required]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır.")]
        public decimal Amount { get; set; }
    }
}
