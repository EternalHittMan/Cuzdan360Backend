using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "Token zorunludur.")]

    public string Token { get; set; }
}