using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models.Finance;
using Microsoft.EntityFrameworkCore;

namespace Cuzdan360Backend.Services
{
    public class RecurringTransactionWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RecurringTransactionWorker> _logger;

        public RecurringTransactionWorker(IServiceProvider serviceProvider, ILogger<RecurringTransactionWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recurring Transaction Worker başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRecurringTransactionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Recurring Transaction Worker döngüsünde hata oluştu.");
                }

                // Her 24 saatte bir kontrol et (ya da her saat başı kontrol edip saati kontrol edebiliriz).
                // Basit olması için: Bir sonraki günün başlangıcına kadar veya sabit bir süre bekle.
                // Şimdilik 1 saatte bir kontrol edip, eğer bugün çalışmadıysa çalıştır mantığı kuralım.
                // Veya user logic: "DayOfMonth equals today's day".
                
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken); // Günde 2 kere kontrol etsin yeterli.
            }
        }

        private async Task ProcessRecurringTransactionsAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var today = DateTime.UtcNow.Day;
                var todayDate = DateTime.UtcNow.Date;

                // Bugün çalışması gereken ve henüz bugün çalışmamış aktif kurallar
                var policies = await context.RecurringTransactions
                    .Where(r => r.IsActive && r.DayOfMonth == today)
                    .ToListAsync(stoppingToken);

                foreach (var policy in policies)
                {
                    // Eğer son çalışma tarihi bugün ise atla
                    if (policy.LastRunDate.HasValue && policy.LastRunDate.Value.Date == todayDate)
                    {
                        continue;
                    }

                    _logger.LogInformation("Tekrarlayan işlem çalıştırılıyor: {Title} (User: {UserId})", policy.Title, policy.UserId);

                    var newTransaction = new Transaction
                    {
                        UserId = policy.UserId,
                        Title = policy.Title + " (Otomatik)",
                        Amount = policy.Amount,
                        CategoryId = policy.CategoryId,
                        SourceId = policy.SourceId,
                        AssetTypeId = policy.AssetTypeId,
                        TransactionType = (TransactionType)policy.TransactionType,
                        TransactionDate = DateTime.UtcNow
                    };

                    context.Transactions.Add(newTransaction);
                    policy.LastRunDate = DateTime.UtcNow;
                }

                if (policies.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("{Count} adet tekrarlayan işlem oluşturuldu.", policies.Count);
                }
            }
        }
    }
}
