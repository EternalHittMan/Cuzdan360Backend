using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class VerifyMfaRequest
{
    [Required(ErrorMessage = "Email zorunludur.")]

    public string Email { get; set; }

    [Required(ErrorMessage = "OTP Kodu zorunludur.")]

    public string Otp { get; set; }
}