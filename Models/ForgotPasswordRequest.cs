using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email zorunludur.")]

    public string Email { get; set; }
}