using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Cuzdan360Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    [HttpGet]
    [Authorize] // Sadece kimlik doğrulaması yapılmış kullanıcılar erişebilir
    public IActionResult GetUserEmail()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        return Ok(email);
    }
}