using System.ComponentModel.DataAnnotations;

namespace Cuzdan360Backend.Models;

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]

    public string Username { get; set; }

    [Required(ErrorMessage = "Email zorunludur.")]

    public string Email { get; set; }
}