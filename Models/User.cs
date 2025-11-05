using Cuzdan360Backend.Models.Finance; // 👈 1. EKLENDİ
using System.Collections.Generic;      // 👈 2. EKLENDİ (ICollection için)

namespace Cuzdan360Backend.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
    public string? ResetToken { get; set; } // Şifre sıfırlama token'ı
    public DateTime? ResetTokenExpiry { get; set; } // Token'ın geçerlilik süresi

    public int Balance { get; set; }

    public int Permission { get; set; }

    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    public string? MfaCode { get; set; }

    public string? PendingEmail { get; set; }

    public DateTime? MfaCodeExpiry { get; set; }

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastPasswordChangeDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? LastProfileUpdateDate { get; set; }
    public bool IsOtpEnabled { get; set; }

    // === 3. EKLENDİ ===
    // Navigation Property (One-to-Many)
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    // === EKLENTİ SONU ===
}