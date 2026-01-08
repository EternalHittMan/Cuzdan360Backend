using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.DTOs;
using Cuzdan360Backend.Models.Finance;
using Cuzdan360Backend.Services;

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(AppDbContext context, MarketDataService marketDataService, ILogger<ReportsController> logger)
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

        [HttpGet("overview")]
        public async Task<ActionResult<FinancialReportDto>> GetFinancialReport()
        {
            try
            {
                int userId = GetCurrentUserId();

                // 1. Fetch Fundamental Data
                var assets = await _context.UserAssets.Include(a => a.AssetType).Where(u => u.UserId == userId).ToListAsync();
                var debts = await _context.UserDebts.Where(u => u.UserId == userId).ToListAsync();
                
                // FETCH 12 MONTHS (Yearly View)
                var historyWindow = DateTime.UtcNow.AddMonths(-12);
                var transactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.AssetType) // Include AssetType for Currency Conversion
                    .Include(t => t.Source)
                    .Where(t => t.UserId == userId && t.TransactionDate >= historyWindow)
                    .ToListAsync();

                // FETCH ALL TIME STATS FOR NET WORTH (Cash Balance)
                // Group by AssetType too, so we can convert currencies
                var allTimeStats = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .GroupBy(t => new { t.TransactionType, Code = t.AssetType != null ? t.AssetType.Code : "TRY" })
                    .Select(g => new { 
                        Type = g.Key.TransactionType, 
                        Currency = g.Key.Code, 
                        Total = g.Sum(t => t.Amount) 
                    })
                    .ToListAsync();
                
                // Old logic removed, moving to new normalization logic below
                // decimal allTimeIncome = allTimeStats.FirstOrDefault(x => x.Type == TransactionType.Income)?.Total ?? 0;
                // decimal allTimeExpense = allTimeStats.FirstOrDefault(x => x.Type == TransactionType.Expense)?.Total ?? 0;
                // decimal cashBalance = allTimeIncome - allTimeExpense;

                var recurring = await _context.RecurringTransactions
                    .Include(r => r.AssetType) // Include AssetType
                    .Where(r => r.UserId == userId && r.IsActive)
                    .ToListAsync();

                // 2. LIVE ASSET VALUATION
                // 2. LIVE ASSET VALUATION & CURRENCY MAP
                var symbolMap = new Dictionary<string, string>();
                
                // Collect symbols from Assets
                foreach (var asset in assets)
                {
                    // If asset has a specific symbol (e.g. THYAO.IS), use it
                    if (!string.IsNullOrEmpty(asset.Symbol))
                         symbolMap[asset.Symbol] = asset.Symbol;
                    // Also track the general currency type
                    if (asset.AssetType != null && !string.IsNullOrEmpty(asset.AssetType.Code))
                         symbolMap[asset.AssetType.Code] = asset.AssetType.Code;
                }
                // Collect symbols from Transactions (All Time)
                foreach(var stat in allTimeStats) 
                    if(!string.IsNullOrEmpty(stat.Currency) && stat.Currency != "TRY") symbolMap[stat.Currency] = stat.Currency;

                // Ensure base pairs are present
                if(!symbolMap.ContainsKey("USD")) symbolMap["USD"] = "USDTRY=X";
                if(!symbolMap.ContainsKey("EUR")) symbolMap["EUR"] = "EURTRY=X";
                if(!symbolMap.ContainsKey("GA")) symbolMap["GA"] = "XAUTRY=X"; // Gram Gold

                // Fix map for API (e.g. USD -> USDTRY=X)
                var querySymbols = new List<string>();
                foreach(var kv in symbolMap)
                {
                     string s = kv.Key;
                     if(s == "USD") s = "USDTRY=X";
                     else if(s == "EUR") s = "EURTRY=X";
                     else if(s == "GA" || s == "XAU" || s == "XAUTRY") s = "XAUTRY=X";
                     else if(s == "BTC") s = "BTC-USD";
                     
                     if(!querySymbols.Contains(s)) querySymbols.Add(s);
                }

                var livePrices = await _marketDataService.GetCurrentPricesAsync(querySymbols);

                // Rate Helper
                decimal GetRate(string currencyCode)
                {
                     if(string.IsNullOrEmpty(currencyCode) || currencyCode == "TRY") return 1m;
                     
                     string targetKey = currencyCode;
                     if(currencyCode == "USD") targetKey = "USDTRY=X";
                     else if(currencyCode == "EUR") targetKey = "EURTRY=X";
                     else if(currencyCode == "GA" || currencyCode == "XAU" || currencyCode == "XAUTRY") targetKey = "XAUTRY=X";
                     else if(currencyCode == "BTC") targetKey = "BTC-USD";

                     if(livePrices.TryGetValue(targetKey, out var data))
                     {
                         // Handle Cross Rates (e.g. BTC is in USD)
                         if(data.Currency == "USD" && targetKey != "USDTRY=X") 
                             return data.Price * (livePrices.TryGetValue("USDTRY=X", out var usd) ? usd.Price : 34m);
                         return data.Price;
                     }
                     // Fallback
                     if(currencyCode == "USD") return 34m;
                     if(currencyCode == "EUR") return 36m;
                     return 1m;
                }

                // Calculate Net Worth Cash Balance (Normalized)
                decimal allTimeIncome = 0;
                decimal allTimeExpense = 0;
                foreach(var stat in allTimeStats)
                {
                    decimal val = stat.Total * GetRate(stat.Currency);
                    if(stat.Type == TransactionType.Income) allTimeIncome += val;
                    else allTimeExpense += val;
                }
                decimal cashBalance = allTimeIncome - allTimeExpense;


                decimal totalAssetsValue = 0;
                decimal totalCostBasis = 0;
                var assetAllocations = new Dictionary<string, decimal>();

                foreach (var asset in assets)
                {
                    decimal price = 1; 
                    
                    string code = asset.AssetType?.Code ?? "TRY";
                    
                    // Specific Logic for Asset Pricing (Prioritize Symbol -> AssetType -> GetRate)
                    string lookupKey = !string.IsNullOrEmpty(asset.Symbol) ? asset.Symbol : asset.AssetType?.Code;

                    if(!string.IsNullOrEmpty(lookupKey) && livePrices.ContainsKey(lookupKey))
                         price = livePrices[lookupKey].Price;
                    else if(!string.IsNullOrEmpty(asset.AssetType?.Code) && livePrices.ContainsKey(asset.AssetType.Code))
                         price = livePrices[asset.AssetType.Code].Price;
                    else
                         price = GetRate(code);

                    decimal val = asset.Amount * price;
                    totalAssetsValue += val;
                    totalCostBasis += (asset.Amount * asset.AverageCost);

                    string typeName = asset.AssetType?.Name ?? "Diğer";
                    if (!assetAllocations.ContainsKey(typeName)) assetAllocations[typeName] = 0;
                    assetAllocations[typeName] += val;
                }

                // 3. DEBT CALCULATION
                decimal totalDebtValue = debts.Sum(d => d.Amount);

                // 4. CASHFLOW ANALYSIS (Normalized)
                // Normalize Transactions List
                var normalizedTransactions = transactions.Select(t => new {
                    Original = t,
                    NormalizedAmount = t.Amount * GetRate(t.AssetType?.Code)
                }).ToList();

                var expenses = normalizedTransactions.Where(x => x.Original.TransactionType == TransactionType.Expense).ToList();
                var incomes = normalizedTransactions.Where(x => x.Original.TransactionType == TransactionType.Income).ToList();
                
                // Burn Rate (Last 6 months avg)
                var recentExpenses = expenses.Where(x => x.Original.TransactionDate >= DateTime.UtcNow.AddMonths(-6)).ToList();
                decimal totalExpenseLast6Mo = recentExpenses.Sum(x => x.NormalizedAmount);
                decimal monthlyBurnRate = totalExpenseLast6Mo / 6.0m;
                if (monthlyBurnRate == 0) monthlyBurnRate = 1; 

                // Survival Months (Runway)
                double survivalMonths = monthlyBurnRate > 0 ? (double)((totalAssetsValue * 0.8m) / monthlyBurnRate) : 999;
                
                // Savings Rate (Last 6 months)
                var recentIncomes = incomes.Where(x => x.Original.TransactionDate >= DateTime.UtcNow.AddMonths(-6)).ToList();
                decimal totalIncomeLast6Mo = recentIncomes.Sum(x => x.NormalizedAmount);
                decimal savingsRate = totalIncomeLast6Mo > 0 ? ((totalIncomeLast6Mo - totalExpenseLast6Mo) / totalIncomeLast6Mo) * 100 : 0;

                // 5. TRENDS HISTORY (Normalized)
                var monthlyHistory = normalizedTransactions
                    .GroupBy(x => new { x.Original.TransactionDate.Year, x.Original.TransactionDate.Month })
                    .Select(g => new MonthlyHistoryDto
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Income = g.Where(x => x.Original.TransactionType == TransactionType.Income).Sum(x => x.NormalizedAmount),
                        Expense = g.Where(x => x.Original.TransactionType == TransactionType.Expense).Sum(x => x.NormalizedAmount),
                        NetChange = g.Where(x => x.Original.TransactionType == TransactionType.Income).Sum(x => x.NormalizedAmount) - 
                                    g.Where(x => x.Original.TransactionType == TransactionType.Expense).Sum(x => x.NormalizedAmount)
                    })
                    .OrderBy(m => DateTime.Parse(m.Month))
                    .ToList();

                // 5.5 CATEGORY TRENDS (Normalized)
                var categoryTrends = normalizedTransactions
                    .Where(x => x.Original.TransactionType == TransactionType.Expense)
                    .GroupBy(x => new { x.Original.TransactionDate.Year, x.Original.TransactionDate.Month })
                    .Select(g => {
                        var amounts = g.GroupBy(x => x.Original.Category?.Name ?? "Diğer")
                                       .ToDictionary(cg => cg.Key, cg => cg.Sum(x => x.NormalizedAmount));
                        return new CategoryTrendDto
                        {
                            Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                            CategoryAmounts = amounts
                        };
                    })
                    .OrderBy(c => DateTime.Parse(c.Month))
                    .ToList();

                // 6. UPCOMING PAYMENTS
                var today = DateTime.UtcNow.Date;
                var upcoming = new List<ReportUpcomingPaymentDto>();

                foreach (var rec in recurring)
                {
                    decimal normalizedRecAmount = rec.Amount * GetRate(rec.AssetType?.Code);
                    DateTime nextDate = DateTime.MinValue;
                    if (rec.Frequency == 0) // Monthly
                    {
                        var targetDay = rec.DayOfMonth;
                        var candidate = new DateTime(today.Year, today.Month, Math.Min(targetDay, DateTime.DaysInMonth(today.Year, today.Month)));
                        if (candidate < today) candidate = candidate.AddMonths(1);
                        nextDate = candidate;
                    }
                    else 
                    {
                         nextDate = today.AddDays(7); 
                    }

                    if ((nextDate - today).TotalDays <= 30)
                    {
                        upcoming.Add(new ReportUpcomingPaymentDto
                        {
                            Title = rec.Title,
                            Amount = normalizedRecAmount, // Use normalized amount
                            DueDate = nextDate,
                            DaysLeft = (int)(nextDate - today).TotalDays
                        });
                    }
                }
                upcoming = upcoming.OrderBy(u => u.DaysLeft).ToList();

                // 7. FINANCIAL HEALTH & IDENTITY LOGIC
                int liquidityScore = survivalMonths > 6 ? 100 : (int)((survivalMonths / 6.0) * 100);
                if (survivalMonths == 999) liquidityScore = 100;

                int solvencyScore = 100;
                if (totalAssetsValue > 0)
                {
                    decimal debtRatio = totalDebtValue / totalAssetsValue;
                    if (debtRatio >= 1) solvencyScore = 0;
                    else solvencyScore = (int)((1 - debtRatio) * 100);
                }
                else if (totalDebtValue > 0)
                {
                    solvencyScore = 0;
                }

                int growthScore = savingsRate <= 0 ? 0 : (savingsRate >= 20 ? 100 : (int)((savingsRate / 20.0m) * 100));

                int typeCount = assetAllocations.Count;
                int diversificationScore = typeCount * 20; 
                if (totalAssetsValue > 0 && assetAllocations.Any(x => (x.Value / totalAssetsValue) > 0.7m))
                {
                    diversificationScore -= 20;
                }
                diversificationScore = Math.Clamp(diversificationScore, 0, 100);

                int stabilityScore = 75; 

                string identity = "Çırak";
                double avgScore = (liquidityScore + solvencyScore + growthScore + diversificationScore) / 4.0;
                if (avgScore > 85) identity = "Finansal İmparator";
                else if (avgScore > 70) identity = "Stratejist";
                else if (avgScore > 50) identity = "İnşaatçı";
                else if (avgScore > 30) identity = "Toparlayıcı";
                
                var financialHealth = new FinancialHealthDto
                {
                    LiquidityScore = liquidityScore,
                    SolvencyScore = solvencyScore,
                    GrowthScore = growthScore,
                    StabilityScore = stabilityScore,
                    DiversificationScore = diversificationScore
                };

                // === 7.5. NEW ANALYTICS LOGIC ===
                
                // A. Expense Structure (Sabit vs Değişken)
                decimal monthlyFixedCost = recurring.Sum(r => r.Amount * GetRate(r.AssetType?.Code)); 
                decimal avgMonthlyIncome = Math.Max(totalIncomeLast6Mo / 6.0m, 1);

                decimal variableCost = Math.Max(monthlyBurnRate - monthlyFixedCost, 0);
                decimal monthlySavingsAmount = Math.Max(avgMonthlyIncome - monthlyBurnRate, 0);

                // Flexibility Score
                int flexibilityScore = 0;
                if (avgMonthlyIncome > 0)
                {
                    decimal lockedRatio = monthlyFixedCost / avgMonthlyIncome;
                    flexibilityScore = (int)((1 - Math.Min(lockedRatio, 1)) * 100);
                }

                var expenseStructure = new ExpenseStructureDto
                {
                    FixedCosts = monthlyFixedCost,
                    VariableCosts = variableCost,
                    Savings = monthlySavingsAmount,
                    FlexibilityScore = flexibilityScore
                };

                // === NEW: Weekly Spending Rhythm (Normalized) ===
                var daysMap = new Dictionary<DayOfWeek, string> {
                    { DayOfWeek.Monday, "Pazartesi" }, { DayOfWeek.Tuesday, "Salı" }, { DayOfWeek.Wednesday, "Çarşamba" },
                    { DayOfWeek.Thursday, "Perşembe" }, { DayOfWeek.Friday, "Cuma" }, { DayOfWeek.Saturday, "Cumartesi" }, { DayOfWeek.Sunday, "Pazar" }
                };
                
                var weeklyTurnover = normalizedTransactions
                    .Where(x => x.Original.TransactionType == TransactionType.Expense)
                    .GroupBy(x => x.Original.TransactionDate.DayOfWeek)
                    .Select(g => new WeeklyTurnoverDto {
                        DayName = daysMap.ContainsKey(g.Key) ? daysMap[g.Key] : g.Key.ToString(),
                        DayIndex = g.Key == DayOfWeek.Sunday ? 7 : (int)g.Key, // Mon=1 ... Sun=7
                        Amount = g.Sum(x => x.NormalizedAmount)
                    })
                    .OrderBy(w => w.DayIndex)
                    .ToList();

                // === NEW: Income Sources Analysis (Normalized) ===
                var totalInc = incomes.Sum(x => x.NormalizedAmount);
                var incomeSources = incomes
                    .GroupBy(x => x.Original.Source != null ? x.Original.Source.SourceName : "Diğer")
                    .Select(g => new IncomeSourceDto {
                        SourceName = g.Key,
                        Amount = g.Sum(x => x.NormalizedAmount),
                        Percentage = totalInc > 0 ? (g.Sum(x => x.NormalizedAmount) / totalInc) * 100 : 0,
                        ColorCode = "#10b981" // Default Green
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();
                
                // Assign colors
                string[] incColors = { "#10b981", "#3b82f6", "#f59e0b", "#8b5cf6", "#ec4899" };
                for (int i = 0; i < incomeSources.Count; i++) incomeSources[i].ColorCode = incColors[i % incColors.Length];

                // B. Wealth Projection
                var projection = new List<WealthProjectionDto>();
                decimal runningNetWorth = totalAssetsValue - totalDebtValue + cashBalance; // Include Cash!
                
                projection.Add(new WealthProjectionDto { 
                    Date = "Şimdi", 
                    Amount = runningNetWorth, 
                    IsProjected = false 
                });

                // Project 12 Months
                decimal projectedGrowthPerMonth = monthlySavingsAmount;
                for (int i = 1; i <= 12; i++)
                {
                    runningNetWorth += projectedGrowthPerMonth;
                    projection.Add(new WealthProjectionDto {
                        Date = DateTime.UtcNow.AddMonths(i).ToString("MMM yy"),
                        Amount = runningNetWorth,
                        IsProjected = true
                    });
                }

                // 8. BUILD REPORT
                var report = new FinancialReportDto
                {
                    TotalAssets = totalAssetsValue, // Only use explicit assets
                    TotalDebts = totalDebtValue,
                    TotalNetWorth = totalAssetsValue - totalDebtValue, // Match Dashboard logic
                    MonthlyBurnRate = monthlyBurnRate,
                    SurvivalMonths = Math.Round(survivalMonths, 1),
                    SavingsRate = Math.Round(savingsRate, 2),
                    
                    TotalProfitLoss = totalAssetsValue - totalCostBasis,
                    TotalProfitLossPercent = totalCostBasis > 0 ? ((totalAssetsValue - totalCostBasis) / totalCostBasis) * 100 : 0,

                    AssetAllocation = assetAllocations.Select(kv => new AssetAllocationDto
                    {
                        AssetType = kv.Key,
                        Value = kv.Value,
                        Percentage = totalAssetsValue > 0 ? (kv.Value / totalAssetsValue) * 100 : 0,
                        ColorCode = GetColorForCategory(kv.Key)
                    }).OrderByDescending(a => a.Value).ToList(),

                    TopExpenseCategories = expenses
                        .GroupBy(e => e.Original.Category?.Name ?? "Uncategorized")
                        .Select(g => new CategorySpendingDto
                        {
                            CategoryName = g.Key,
                            Amount = g.Sum(e => e.NormalizedAmount),
                            Percentage = totalExpenseLast6Mo > 0 ? (g.Sum(e => e.NormalizedAmount) / totalExpenseLast6Mo) * 100 : 0
                        })
                        .OrderByDescending(c => c.Amount)
                        .Take(5)
                        .ToList(),

                    MonthlyLayout = monthlyHistory,
                    CategoryTrends = categoryTrends,
                    UpcomingPayments = upcoming,
                    FinancialHealth = financialHealth,
                    FinancialIdentity = identity,
                    WealthProjection = projection,
                    ExpenseStructure = expenseStructure,
                    WeeklyTurnover = weeklyTurnover,
                    IncomeSources = incomeSources
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Finansal rapor oluşturulurken hata.");
                return StatusCode(500, new { error = "Rapor oluşturulamadı." });
            }
        }

        private string GetColorForCategory(string category)
        {
            return category.ToLower() switch
            {
                "stock" => "#3b82f6", // Blue
                "crypto" => "#f59e0b", // Amber
                "gold" => "#eab308", // Yellow
                "cash" => "#22c55e", // Green
                "forex" => "#10b981", // Emerald
                "debt" => "#ef4444", // Red
                _ => "#6b7280" // Gray
            };
        }
    }
}
