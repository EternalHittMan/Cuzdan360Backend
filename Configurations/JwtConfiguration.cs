using System.Globalization;

namespace Cuzdan360Backend.Configurations;

public class JwtConfiguration
{
    public string Issuer { get; } = string.Empty;
    public string Secret { get; } = string.Empty;
    public string Audience { get; } = string.Empty;
    public int ExpireMinutes { get; } // Token süresi (dakika cinsinden)
    public int RefreshTokenExpireDays { get; } // Refresh token süresi (gün cinsinden)

    public JwtConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("JWT");

        Issuer = section[nameof(Issuer)] ?? throw new ArgumentNullException(nameof(Issuer));
        Secret = section[nameof(Secret)] ?? throw new ArgumentNullException(nameof(Secret));
        Audience = section[nameof(Audience)] ?? throw new ArgumentNullException(nameof(Audience));
        ExpireMinutes = Convert.ToInt32(section[nameof(ExpireMinutes)], CultureInfo.InvariantCulture);
        RefreshTokenExpireDays = Convert.ToInt32(section[nameof(RefreshTokenExpireDays)], CultureInfo.InvariantCulture);
    }
}