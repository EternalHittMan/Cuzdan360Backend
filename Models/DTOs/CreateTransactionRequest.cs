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

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
        public decimal Amount { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }
    }
}