namespace Cuzdan360Backend.Models.Finance;

public enum TransactionType : byte
{
    Income = 0,   // Girdi (örneğin maaş, satış)
    Expense = 1   // Çıktı (örneğin alışveriş, fatura)
}
