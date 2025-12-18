using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.DTOs;
using System.Globalization;
using Cuzdan360Backend.Models.Finance;

namespace Cuzdan360Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(AppDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Geçersiz token. Kullanıcı kimliği bulunamadı.");
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

                // 1. Fetch Data
                var summary = new DashboardSummaryDto();

                var assets = await _context.UserAssets
                    .Include(a => a.AssetType)
                    .Where(a => a.UserId == userId)
                    .AsNoTracking()
                    .ToListAsync();

                // 2. Net Worth Calculation
                decimal totalNetWorth = 0;
                var netWorthDataPoints = new List<ChartDataPointDto>();

                // Mock Rates - In a real app, inject a CurrencyService
                // 1 USD = 34.0 TRY, 1 EUR = 36.0 TRY, 1 Gram Gold = 2800 TRY, 1 BTC = 3000000 TRY
                decimal GetRate(string code) => code switch
                {
                    "USD" => 34.0m,
                    "EUR" => 36.0m,
                    "XAUTRY" => 2800.0m, // Gram Gold
                    "BTC" => 3000000.0m,
                    _ => 1.0m // TRY or default
                };

                foreach (var asset in assets)
                {
                    decimal rate = GetRate(asset.AssetType?.Code ?? "TRY");
                    decimal valueInTry = asset.Amount * rate;
                    totalNetWorth += valueInTry;

                    netWorthDataPoints.Add(new ChartDataPointDto
                    {
                        Label = asset.AssetType?.Name ?? "Unknown",
                        Value = valueInTry,
                        Percentage = 0 // Will calc later
                    });
                }
                summary.TotalNetWorth = totalNetWorth;

                // Update percentages for Pie Chart
                if (totalNetWorth > 0)
                {
                    foreach (var point in netWorthDataPoints)
                    {
                        point.Percentage = Math.Round((point.Value / totalNetWorth) * 100, 2);
                    }
                }
                summary.NetWorthDistribution = netWorthDataPoints;

                // 3. Transactions Query
                var startOfMonth = new DateTime(currentYear, currentMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
                
                // Past 6 months
                var startOf6Months = startOfMonth.AddMonths(-5);

                var transactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.Source)
                    .Where(t => t.UserId == userId && t.TransactionDate >= startOf6Months) // Fetch 6 months to cover all needs
                    .AsNoTracking()
                    .ToListAsync();

                // 3a. Monthly Income vs Expense (Current Month)
                var currentMonthTransactions = transactions
                    .Where(t => t.TransactionDate >= startOfMonth && t.TransactionDate <= endOfMonth)
                    .ToList();

                summary.MonthlyIncome = currentMonthTransactions
                    .Where(t => t.TransactionType == TransactionType.Income) // Income
                    .Sum(t => t.Amount);

                summary.MonthlyExpense = currentMonthTransactions
                    .Where(t => t.TransactionType == TransactionType.Expense) // Expense
                    .Sum(t => t.Amount);

                // 4. Top Categories (Donut Chart) - Expense Only, Current Month
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
                if (otherAmount > 0)
                {
                    topCategories.Add(new CategoryExpenseDto { CategoryName = "Diğer", Amount = otherAmount });
                }
                summary.TopCategories = topCategories;

                // 5. 6-Month Trend (Line Chart)
                var trend = transactions
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                    .Select(g => new MonthlyTrendDto
                    {
                        Year = g.Key.Year,
                        Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        TotalIncome = g.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount),
                        TotalExpense = g.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount)
                    })
                    .OrderBy(x => x.Year).ThenBy(x => DateTime.ParseExact(x.Month, "MMMM", CultureInfo.CurrentCulture).Month) // Sort tricky if culture varies, simple int sort better usually but req says DTO has string Month
                    .ToList();
                
                // Backfill missing months if needed, but for now simple group
                summary.Last6MonthsTrend = trend;


                // 6. Source Flows (Bar Chart) - All fetched time range or just this month? 
                // Context says "Group Transactions", usually implies current view context (Month), 
                // but usually "Flow" implies tracking liquidity. Let's use Current Month for specific context.
                var sourceFlows = currentMonthTransactions
                    .Where(t => t.Source != null)
                    .GroupBy(t => t.Source!.SourceName)
                    .Select(g => new SourceFlowDto
                    {
                        SourceName = g.Key,
                        NetFlow = g.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount) - 
                                  g.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount)
                    })
                    .ToList();
                summary.SourceFlows = sourceFlows;

                // 7. Upcoming Payments
                var recurring = await _context.RecurringTransactions
                    .Where(r => r.UserId == userId && r.IsActive)
                    .AsNoTracking()
                    .ToListAsync();

                var upcoming = new List<UpcomingPaymentDto>();
                foreach (var r in recurring)
                {
                    // Calculate next payment date
                    // If DayOfMonth has passed for this month, assume next month.
                    var today = DateTime.UtcNow.Day;
                    DateTime nextDate;

                    if (r.DayOfMonth >= today)
                    {
                        // Still to come this month
                        try {
                             nextDate = new DateTime(currentYear, currentMonth, Math.Min(r.DayOfMonth, DateTime.DaysInMonth(currentYear, currentMonth)));
                        } catch { nextDate = now; } // Fail safe
                    }
                    else
                    {
                        // Next month
                        var nextMonthDate = now.AddMonths(1);
                        try {
                            nextDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, Math.Min(r.DayOfMonth, DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month)));
                        } catch { nextDate = now; }
                    }

                    var daysRemaining = (nextDate.Date - now.Date).Days;
                    
                    upcoming.Add(new UpcomingPaymentDto
                    {
                        Title = r.Title,
                        Amount = r.Amount,
                        NextPaymentDate = nextDate,
                        DaysRemaining = daysRemaining
                    });
                }
                summary.UpcomingPayments = upcoming.OrderBy(x => x.DaysRemaining).Take(5).ToList();

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
