using Cuzdan360Backend.Models.Finance;

namespace Cuzdan360Backend.Models.DTOs
{
    public class FinancialReportDto
    {
        // === VITAL SIGNS (Hayatta Kalma & Sağlık) ===
        public decimal TotalNetWorth { get; set; }      // Net Varlık (Varlıklar - Borçlar)
        public decimal TotalAssets { get; set; }        // Toplam Varlık Değeri
        public decimal TotalDebts { get; set; }         // Toplam Borç
        
        // Runway: (Likit Varlıklar) / (Aylık Ortalama Gider)
        // Hiç gelir olmasa kaç ay idare edebilir?
        public double SurvivalMonths { get; set; }
        
        public decimal MonthlyBurnRate { get; set; }    // Ortalama Aylık Gider
        public decimal SavingsRate { get; set; }        // Tasarruf Oranı (%) (Gelir - Gider) / Gelir

        // === PERFORMANCE (Alpha & Growth) ===
        public decimal TotalProfitLoss { get; set; }    // Toplam Kâr/Zarar (Nominal)
        public decimal TotalProfitLossPercent { get; set; } // Yüzdesel Getiri
        
        // Benchmarks could go here later (Inflation vs Portfolio, etc.)

        // === COMPOSITION (Dağılım) ===
        public List<AssetAllocationDto> AssetAllocation { get; set; } = new();
        public List<CategorySpendingDto> TopExpenseCategories { get; set; } = new();
        public List<IncomeSourceDto> IncomeSources { get; set; } = new(); // Gelir Kaynakları

        // === BEHAVIOR (Davranış - Yeni) ===
        public List<WeeklyTurnoverDto> WeeklyTurnover { get; set; } = new(); // Harcama Ritmi

        // === HEALTH (Radar Chart) ===
        public FinancialHealthDto FinancialHealth { get; set; } = new();
        public string FinancialIdentity { get; set; } = "Analiz Ediliyor...";

        // === TRENDS (Zaman Makinesi & Analiz) ===
        public List<MonthlyHistoryDto> MonthlyLayout { get; set; } = new();
        public List<CategoryTrendDto> CategoryTrends { get; set; } = new();

        // === ADVANCED ANALYTICS (Yeni - Detaylı Analiz) ===
        public List<WealthProjectionDto> WealthProjection { get; set; } = new(); // Gelecek 1 yıl tahmini
        public ExpenseStructureDto ExpenseStructure { get; set; } = new(); // Sabit vs Değişken gider analizi

        // === UPCOMING (Gelecek) ===
        public List<ReportUpcomingPaymentDto> UpcomingPayments { get; set; } = new();
    }

    public class CategoryTrendDto
    {
        public string Month { get; set; }
        public Dictionary<string, decimal> CategoryAmounts { get; set; } = new();
    }

    public class WealthProjectionDto
    {
        public string Date { get; set; }
        public decimal Amount { get; set; }
        public bool IsProjected { get; set; } // True ise kesik çizgi (tahmin)
    }

    public class ExpenseStructureDto
    {
        public decimal FixedCosts { get; set; } // Sabit (Recurring)
        public decimal VariableCosts { get; set; } // Değişken
        public decimal Savings { get; set; } // Kalan (Tasarruf)
        public int FlexibilityScore { get; set; } // 0-100 (Düşük sabit gider = Yüksek esneklik)
    }

    public class FinancialHealthDto
    {
        public int LiquidityScore { get; set; } // Likidite
        public int SolvencyScore { get; set; }  // Borç Ödeme Gücü
        public int GrowthScore { get; set; }    // Büyüme (Tasarruf/Yatırım)
        public int StabilityScore { get; set; } // İstikrar (Düzenli Gelir/Gider)
        public int DiversificationScore { get; set; } // Çeşitlilik
    }

    public class AssetAllocationDto
    {
        public string AssetType { get; set; }
        public decimal Value { get; set; }
        public decimal Percentage { get; set; }
        public string ColorCode { get; set; } // Frontend için renk kodu
    }

    public class CategorySpendingDto
    {
        public string CategoryName { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class MonthlyHistoryDto
    {
        public string Month { get; set; } // "Jan 2024"
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal NetChange { get; set; } // Income - Expense
    }

    public class ReportUpcomingPaymentDto
    {
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysLeft { get; set; }
    }

    public class WeeklyTurnoverDto
    {
        public string DayName { get; set; } // "Pazartesi"
        public decimal Amount { get; set; }
        public int DayIndex { get; set; } // 1 (Mon) - 7 (Sun)
    }

    public class IncomeSourceDto
    {
        public string SourceName { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        public string ColorCode { get; set; }
    }
}
