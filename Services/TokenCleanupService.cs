using Cuzdan360Backend.Data;

namespace Cuzdan360Backend.Services;

public class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public TokenCleanupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try 
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var expiredTokens = dbContext.Users.Where(u => u.RefreshTokenExpiry < DateTime.UtcNow).ToList();

                        foreach (var user in expiredTokens)
                        {
                            user.RefreshToken = null;
                            user.RefreshTokenExpiry = null;
                        }

                        await dbContext.SaveChangesAsync(stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception)
                {
                    // Log error but continue
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }
}