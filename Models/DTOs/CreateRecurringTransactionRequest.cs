using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs
{
    public class CreateRecurringTransactionRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır.")]
        public decimal Amount { get; set; }

        public int? CategoryId { get; set; }
        public int? SourceId { get; set; }
        public int? AssetTypeId { get; set; }

        public int TransactionType { get; set; }

        [Range(1, 31, ErrorMessage = "Gün 1 ile 31 arasında olmalıdır.")]
        public int DayOfMonth { get; set; }
    }
}
