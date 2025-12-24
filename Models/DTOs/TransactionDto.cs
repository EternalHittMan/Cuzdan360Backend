using Cuzdan360Backend.Models.Finance;

namespace Cuzdan360Backend.Models.DTOs
{
    public class TransactionDto
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public int AssetTypeId { get; set; }
        public int CategoryId { get; set; }
        public int SourceId { get; set; }
        public TransactionType TransactionType { get; set; }
        public decimal Amount { get; set; }
        public string? Title { get; set; }
        public DateTime TransactionDate { get; set; }
        
        // Flattened properties for display
        public string? CategoryName { get; set; }
        public string? SourceName { get; set; }
        public string? AssetTypeName { get; set; }
        public string? AssetTypeCode { get; set; }
    }
}
