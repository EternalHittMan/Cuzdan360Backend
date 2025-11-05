using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Token zorunludur.")]

    public string RefreshToken { get; set; }
}