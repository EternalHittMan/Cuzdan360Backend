using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    public string Password { get; set; } = string.Empty;
}