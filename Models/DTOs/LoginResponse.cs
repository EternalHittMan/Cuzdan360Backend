using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs;

public class LoginResponse
{
    public string Token { get; set; }
    public bool RequiresOtp { get; set; }
}