using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs;

public class LoginResponse
{
    public string Token { get; set; }
    public bool RequiresOtp { get; set; }
    public bool IsEmailVerified { get; set; }
    public string Email { get; set; }
    public string RefreshToken { get; set; } // 👈 Eklendi
}