using System;
using System.Collections.Generic;
using Cuzdan360Backend.Models.Finance;

namespace Cuzdan360Backend.Models.DTOs
{
    public class ExtractedTransactionDto
    {
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public int TransactionType { get; set; } // 0=Income, 1=Expense

        // AI Suggested Fields
        public string SuggestedCategory { get; set; }
        public string SuggestedSource { get; set; }
        public string SuggestedAssetType { get; set; }

        // Matched IDs (Nullable)
        public int? CategoryId { get; set; }
        public int? SourceId { get; set; }
        public int? AssetTypeId { get; set; }
    }

    public class BulkCreateTransactionRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("transactions")]
        public List<CreateTransactionRequest> Transactions { get; set; }
    }
}
