using BCrypt.Net;

namespace Cuzdan360Backend.Utilities;

public static class PasswordHasher
{
    private const int WorkFactor = 12; // Daha yüksek değerler daha güvenli, ancak daha yavaş

    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public static bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
}