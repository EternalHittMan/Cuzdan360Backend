using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.DTOs;
using System.Globalization;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Services;

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(AppDbContext context, MarketDataService marketDataService, ILogger<DashboardController> logger)
        {
            _context = context;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Geçersiz kullanıcı kimliği.");
            }
            return userId;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var userId = GetCurrentUserId();
                var now = DateTime.UtcNow;
                var currentMonth = now.Month;
                var currentYear = now.Year;

                var summary = new DashboardSummaryDto();

                // 1. Fetch Assets & Debts
                var assets = await _context.UserAssets
                    .Include(a => a.AssetType)
                    .Where(a => a.UserId == userId)
                    .AsNoTracking()
                    .ToListAsync();

                var debts = await _context.UserDebts
                    .Include(d => d.AssetType)
                    .Where(d => d.UserId == userId)
                    .AsNoTracking()
                    .ToListAsync();

                // 2. Prepare for Live Data
                var symbolsToFetch = new HashSet<string> { "USDTRY=X", "EURTRY=X", "GBPTRY=X", "XAUTRY=X", "BTC-USD" }; // Base rates
                
                foreach (var asset in assets)
                {
                    if (!string.IsNullOrEmpty(asset.Symbol)) symbolsToFetch.Add(asset.Symbol);
                    // Add logic to Map AssetType.Code to generic ticker if symbol is missing (e.g. USD -> USDTRY=X)
                    if (string.IsNullOrEmpty(asset.Symbol) && asset.AssetType?.Code != "TRY")
                    {
                        if (asset.AssetType?.Code == "USD") symbolsToFetch.Add("USDTRY=X");
                        if (asset.AssetType?.Code == "EUR") symbolsToFetch.Add("EURTRY=X");
                        if (asset.AssetType?.Code == "GA" || asset.AssetType?.Code == "XAU") symbolsToFetch.Add("XAUTRY=X");
                    }
                }

                var marketData = await _marketDataService.GetCurrentPricesAsync(symbolsToFetch.ToList());

                // Capture Base Rates
                decimal usdTryRate = marketData.TryGetValue("USDTRY=X", out var usdData) ? usdData.Price : 34.0m;
                decimal eurTryRate = marketData.TryGetValue("EURTRY=X", out var eurData) ? eurData.Price : 36.0m;
                
                // Helper to convert any value to TRY
                decimal ConvertToTry(decimal amount, string currency)
                {
                    if (currency == "TRY") return amount;
                    if (currency == "USD") return amount * usdTryRate;
                    if (currency == "EUR") return amount * eurTryRate;
                    if (currency == "GA" || currency == "XAU") 
                        return amount * (marketData.TryGetValue("XAUTRY=X", out var gold) ? gold.Price : 2800);
                    // Fallback for others if we don't have rate map (assume USD for crypto/international?)
                    return amount * usdTryRate; 
                }

                // 3. Calculate Assets Total & Grouping for Pie Chart
                decimal totalAssetsTry = 0;
                var assetGroups = new Dictionary<string, decimal>();

                foreach (var asset in assets)
                {
                    decimal assetValueTry = 0;

                    if (!string.IsNullOrEmpty(asset.Symbol) && marketData.TryGetValue(asset.Symbol, out var data))
                    {
                        // Asset has specific symbol (e.g. THYAO.IS, AAPL)
                        decimal price = data.Price;
                        // If price is in USD (e.g. AAPL), convert to TRY
                        if (data.Currency == "USD") price *= usdTryRate;
                        else if (data.Currency == "EUR") price *= eurTryRate;
                        
                        assetValueTry = asset.Amount * price;
                    }
                    else
                    {
                        // Generic Asset using Helper
                        string code = asset.AssetType?.Code ?? "TRY";
                        assetValueTry = ConvertToTry(asset.Amount, code);
                    }

                    totalAssetsTry += assetValueTry;

                    // Grouping Logic (Match Ultrathink)
                    string category = asset.AssetCategory;
                    
                    // Fallback grouping if category is empty or "Other" (legacy data)
                    if (string.IsNullOrEmpty(category) || category.Equals("Other", StringComparison.OrdinalIgnoreCase))
                    {
                         string typeName = asset.AssetType?.Name?.ToLower() ?? "";
                         string typeCode = asset.AssetType?.Code?.ToUpper() ?? "";

                         if (typeCode == "TRY" || typeCode == "USD" || typeCode == "EUR" || typeCode == "GBP") category = "Nakit Varlıklar";
                         else if (typeCode.Contains("XAU") || typeName.Contains("altın") || typeName.Contains("gümüş")) category = "Emtia & Kıymetli Madenler";
                         else if (typeName.Contains("hisse") || typeName.Contains("stock")) category = "Hisse Senetleri";
                         else if (typeCode.Contains("BTC") || typeCode.Contains("ETH") || typeName.Contains("kripto")) category = "Kripto Paralar";
                         else if (typeName.Contains("fon") || typeName.Contains("fund")) category = "Yatırım Fonları";
                         else 
                         {
                             // If still falling to Other, try to be more specific using Name
                             category = $"Diğer ({asset.AssetType?.Name})";
                         }
                    }
                    
                    _logger.LogInformation($"Asset: {asset.AssetType?.Name} ({asset.AssetType?.Code}) -> Category: {category}");

                    if (!assetGroups.ContainsKey(category)) assetGroups[category] = 0;
                    assetGroups[category] += assetValueTry;
                }
                summary.TotalAssets = totalAssetsTry;

                // 4. Calculate Debts Total
                decimal totalDebtsTry = 0;
                foreach (var debt in debts)
                {
                    string currency = debt.CurrencySymbol ?? debt.AssetType?.Code ?? "TRY";
                    decimal debtValueTry = ConvertToTry(debt.Amount, currency);
                    totalDebtsTry += debtValueTry;
                }
                summary.TotalDebts = totalDebtsTry;

                // 5. Net Worth & Cash Balance
                
                // Transactions Cash Flow
                var allTimeStats = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .GroupBy(t => t.TransactionType)
                    .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
                    .ToListAsync();

                decimal totalIncome = allTimeStats.FirstOrDefault(x => x.Type == TransactionType.Income)?.Total ?? 0;
                decimal totalExpense = allTimeStats.FirstOrDefault(x => x.Type == TransactionType.Expense)?.Total ?? 0;
                decimal cashBalance = totalIncome - totalExpense;

                decimal totalNetWorth = totalAssetsTry - totalDebtsTry + cashBalance;
                summary.TotalNetWorth = totalNetWorth;

                // Add Cash to Asset Groups for the Chart
                if (cashBalance > 0)
                {
                    if (!assetGroups.ContainsKey("Nakit Varlıklar")) assetGroups["Nakit Varlıklar"] = 0;
                    assetGroups["Nakit Varlıklar"] += cashBalance;
                }
                
                // Add Debts to Asset Groups for the Chart (User Request: Visualize Debt Magnitude)
                if (totalDebtsTry > 0)
                {
                     assetGroups["Toplam Borçlar"] = totalDebtsTry;
                }

                // Convert Groups to ChartDataPoints
                var totalChartValue = assetGroups.Values.Sum();
                var netWorthDataPoints = assetGroups.Select(kvp => new ChartDataPointDto
                {
                    Label = kvp.Key,
                    Value = kvp.Value,
                    Percentage = totalChartValue > 0 ? Math.Round((kvp.Value / totalChartValue) * 100, 2) : 0
                }).OrderByDescending(x => x.Value).ToList();

                summary.NetWorthDistribution = netWorthDataPoints;

                // ... (Rest of logic: Monthly Income/Expense, Recent Transactions, etc. is same)
                
                 // 3. Transactions Query
                var startOfMonth = new DateTime(currentYear, currentMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
                var startOf6Months = startOfMonth.AddMonths(-5);

                var transactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.Source)
                    .Where(t => t.UserId == userId && t.TransactionDate >= startOf6Months)
                    .AsNoTracking()
                    .ToListAsync();

                var currentMonthTransactions = transactions
                    .Where(t => t.TransactionDate >= startOfMonth && t.TransactionDate <= endOfMonth)
                    .ToList();

                summary.MonthlyIncome = currentMonthTransactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount);
                summary.MonthlyExpense = currentMonthTransactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount);

                // Top Categories
                var categoryGroups = currentMonthTransactions
                    .Where(t => t.TransactionType == TransactionType.Expense && t.Category != null)
                    .GroupBy(t => t.Category!.Name)
                    .Select(g => new { Name = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var topCategories = categoryGroups.Take(5).Select(x => new CategoryExpenseDto
                {
                    CategoryName = x.Name,
                    Amount = x.Total
                }).ToList();

                var otherAmount = categoryGroups.Skip(5).Sum(x => x.Total);
                if (otherAmount > 0) topCategories.Add(new CategoryExpenseDto { CategoryName = "Diğer", Amount = otherAmount });
                summary.TopCategories = topCategories;

                // Trend
                 var trend = transactions
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                    .Select(g => new MonthlyTrendDto
                    {
                        Year = g.Key.Year,
                        Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        TotalIncome = g.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount),
                        TotalExpense = g.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount)
                    })
                    .OrderBy(x => x.Year).ThenBy(x => DateTime.ParseExact(x.Month, "MMMM", CultureInfo.CurrentCulture).Month)
                    .ToList();
                summary.Last6MonthsTrend = trend;

                // Source Flows
                 summary.SourceFlows = currentMonthTransactions
                    .Where(t => t.Source != null)
                    .GroupBy(t => t.Source!.SourceName)
                    .Select(g => new SourceFlowDto
                    {
                        SourceName = g.Key,
                        NetFlow = g.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount) - 
                                  g.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount)
                    })
                    .ToList();

                // Upcoming Payments (Same logic)
                 var recurring = await _context.RecurringTransactions
                    .Where(r => r.UserId == userId && r.IsActive).AsNoTracking().ToListAsync();
                 
                 var upcoming = new List<UpcomingPaymentDto>();
                 foreach (var r in recurring)
                 {
                     var today = DateTime.UtcNow.Day;
                     DateTime nextDate;
                     if (r.DayOfMonth >= today)
                        try { nextDate = new DateTime(currentYear, currentMonth, Math.Min(r.DayOfMonth, DateTime.DaysInMonth(currentYear, currentMonth))); } catch { nextDate = now; }
                     else
                     {
                        var nextMonthDate = now.AddMonths(1);
                        try { nextDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, Math.Min(r.DayOfMonth, DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month))); } catch { nextDate = now; }
                     }
                     upcoming.Add(new UpcomingPaymentDto { Title = r.Title, Amount = r.Amount, NextPaymentDate = nextDate, DaysRemaining = (nextDate.Date - now.Date).Days });
                 }
                summary.UpcomingPayments = upcoming.OrderBy(x => x.DaysRemaining).Take(5).ToList();

                // Recent Transactions
                 summary.RecentTransactions = transactions.OrderByDescending(t => t.TransactionDate).Take(5)
                    .Select(t => new TransactionDto
                    {
                        TransactionId = t.TransactionId, Title = t.Title, Amount = t.Amount, TransactionDate = t.TransactionDate,
                        TransactionType = t.TransactionType, CategoryName = t.Category?.Name ?? "Diğer", SourceName = t.Source?.SourceName
                    }).ToList();

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard özeti alınırken hata oluştu.");
                return StatusCode(500, new { error = "Dashboard verileri yüklenemedi." });
            }
        }
    }
}
