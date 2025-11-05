using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class LoginWithEmailRequest
{
    [Required(ErrorMessage = "Email zorunludur.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    public string Password { get; set; } = string.Empty;
}