using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Token zorunludur.")]
    public string Token { get; set; }

    [Required(ErrorMessage = "Yeni şifre zorunludur.")]

    public string NewPassword { get; set; }
}