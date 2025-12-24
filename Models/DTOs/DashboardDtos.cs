namespace Cuzdan360Backend.Models.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal TotalNetWorth { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpense { get; set; }
        public List<ChartDataPointDto> NetWorthDistribution { get; set; } // For Pie Chart
        public List<CategoryExpenseDto> TopCategories { get; set; }       // For Donut/Treemap
        public List<MonthlyTrendDto> Last6MonthsTrend { get; set; }       // For Line Chart
        public List<SourceFlowDto> SourceFlows { get; set; }              // For Horizontal Bar
        public List<UpcomingPaymentDto> UpcomingPayments { get; set; }    // For List/Calendar
        public List<TransactionDto> RecentTransactions { get; set; }      // For Recent Transactions Table
    }

    public class ChartDataPointDto
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public decimal Percentage { get; set; }
    }

    public class CategoryExpenseDto
    {
        public string CategoryName { get; set; }
        public decimal Amount { get; set; }
    }

    public class MonthlyTrendDto
    {
        public string Month { get; set; } // e.g., "January"
        public int Year { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
    }

    public class SourceFlowDto
    {
        public string SourceName { get; set; }
        public decimal NetFlow { get; set; } // Income - Expense
    }

    public class UpcomingPaymentDto
    {
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public DateTime NextPaymentDate { get; set; }
        public int DaysRemaining { get; set; }
    }
}
