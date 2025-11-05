using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
    public string CurrentPassword { get; set; }

    [Required(ErrorMessage = "Yeni şifre zorunludur.")]
    public string NewPassword { get; set; }
}