using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cuzdan360Backend.Configurations;

namespace Cuzdan360Backend.Services;

public class TokenService
{
    private readonly JwtConfiguration _config;

    public TokenService(JwtConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(string id, string email, string permission)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("Permission", permission),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config.Issuer,
            audience: _config.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_config.ExpireMinutes), // Token süresi
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}