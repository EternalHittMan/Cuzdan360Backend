using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs;

public class ResendVerificationEmailRequest
{
    [Required(ErrorMessage = "Email zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
    public string Email { get; set; } = string.Empty;
}
