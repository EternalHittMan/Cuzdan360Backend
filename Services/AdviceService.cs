using System.Text;
using System.Text.Json;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Data; // Ensure we have visibility if needed or Models.Finance is enough


namespace Cuzdan360Backend.Services
{
    public class AdviceService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdviceService> _logger;

        public AdviceService(HttpClient httpClient, IConfiguration configuration, ILogger<AdviceService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetFinancialAdviceAsync(List<Transaction> recentTransactions)
        {
            try
            {
                if (!recentTransactions.Any())
                {
                    return "Henüz yeterli veri yok. Biraz harcama yaptıktan sonra tekrar gel!";
                }

                var apiKey = _configuration["Gemini:ApiKey"];
                var model = _configuration["Gemini:Model"]; // e.g., gemini-1.5-flash
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                // Veriyi özetle
                var summary = recentTransactions
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Diğer")
                    .Select(g => $"{g.Key}: {g.Sum(t => t.Amount):N2} TL")
                    .ToList();
                
                var totalIncome = recentTransactions.Where(t => t.TransactionType == (TransactionType)0).Sum(t => t.Amount);
                var totalExpense = recentTransactions.Where(t => t.TransactionType == (TransactionType)1).Sum(t => t.Amount);


                var contextString = $"Last 30 Days Summary:\n" +
                                    $"Total Income: {totalIncome:N2} TL\n" +
                                    $"Total Expense: {totalExpense:N2} TL\n" +
                                    $"Breakdown by Category:\n{string.Join("\n", summary)}";

                var prompt = "You are a helpful financial advisor. Analyze this monthly spending profile. " +
                             "Give short, specific, and friendly financial advice in Turkish for a SME/Individual. " +
                             "Focus on high-spending categories. Keep it under 150 words.";

                 var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new { text = contextString }
                            }
                        }
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var content = candidates[0].GetProperty("content");
                    var parts = content.GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString() ?? "Tavsiye üretilemedi.";
                    }
                }

                return "Şu an tavsiye üretemiyorum.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AdviceService hatası.");
                return "Tavsiye sistemi şu an çalışmıyor.";
            }
        }
    }
}
