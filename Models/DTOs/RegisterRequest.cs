using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçersiz e-posta adresi.")]
    public string Email { get; set; } = string.Empty;
}