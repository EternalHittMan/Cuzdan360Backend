using System.Text;
using System.Text.Json;
using Cuzdan360Backend.Models.DTOs;
using Cuzdan360Backend.Models.Finance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cuzdan360Backend.Services
{
    public class GeminiReceiptService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiReceiptService> _logger;

        public GeminiReceiptService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiReceiptService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<ExtractedTransactionDto>> AnalyzeReceiptAsync(IFormFile file, 
            IEnumerable<Category> categories, 
            IEnumerable<Source> sources, 
            IEnumerable<AssetType> assetTypes)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("Dosya yüklenmedi.");

                // 1. Dosyayı Base64'e çevir
                string base64Image;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    base64Image = Convert.ToBase64String(ms.ToArray());
                }

                var apiKey = _configuration["Gemini:ApiKey"];
                var model = _configuration["Gemini:Model"];
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                // Context String Hazırla
                var categoryList = string.Join(", ", categories.Select(c => $"{c.CategoryId}:{c.Name}"));
                var sourceList = string.Join(", ", sources.Select(s => $"{s.SourceId}:{s.SourceName}"));
                var assetTypeList = string.Join(", ", assetTypes.Select(a => $"{a.AssetTypeId}:{a.Name}"));

                var prompt = $@"Analyze this receipt image. Return ONLY a JSON Array. 
Extract: Title, Amount, Date (in YYYY-MM-DD format), TransactionType (0=Income, 1=Expense).
Also, select the best matching ID from the provided lists for:
- CategoryId (from: {categoryList})
- SourceId (from: {sourceList})
- AssetTypeId (from: {assetTypeList})

If no match is found for an ID, use null.
Do not include markdown formatting like ```json.";

                // 2. Request Body Hazırla
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = file.ContentType,
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                // 3. API İsteği
                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                
                // 4. Response Parsing
                using var doc = JsonDocument.Parse(responseString);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var content = candidates[0].GetProperty("content");
                    var parts = content.GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        
                        // Temizlik (Markdown vs varsa)
                        text = text.Replace("```json", "").Replace("```", "").Trim();

                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            AllowTrailingCommas = true,
                            ReadCommentHandling = JsonCommentHandling.Skip
                        };
                        
                        var transactions = JsonSerializer.Deserialize<List<ExtractedTransactionDto>>(text, options);
                        return transactions ?? new List<ExtractedTransactionDto>();
                    }
                }

                return new List<ExtractedTransactionDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API hatası.");
                throw;
            }
        }
    }
}
