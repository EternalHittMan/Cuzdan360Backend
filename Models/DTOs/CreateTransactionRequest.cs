// Dosya: Models/DTOs/CreateTransactionRequest.cs

using System.ComponentModel.DataAnnotations;
using Cuzdan360Backend.Models.Finance;

namespace Cuzdan360Backend.Models.DTOs
{
    public class CreateTransactionRequest
    {
        [Required]
        public int? AssetTypeId { get; set; }

        [Required]
        public int? CategoryId { get; set; }

        [Required]
        public int? SourceId { get; set; }

        [Required]
        public TransactionType TransactionType { get; set; } // Income = 0, Expense = 1

        [Range(1, 31, ErrorMessage = "Gün 1 ile 31 arasında olmalıdır.")]
        public int? DayOfMonth { get; set; }

        public bool IsRecurring { get; set; }
        public int? RecurringDay { get; set; }
        public int? Frequency { get; set; } // 0=Monthly, 1=Weekly

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
        public decimal Amount { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }
    }
}