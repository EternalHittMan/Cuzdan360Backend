using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Cuzdan360Backend.Repositories; // 👈 YENİ
using System.Threading.Tasks; // 👈 YENİ
using Cuzdan360Backend.Exceptions; // 👈 YENİ

namespace Cuzdan360Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // 👈 Bu controller'ın tamamı artık kimlik doğrulaması gerektiriyor
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository; // 👈 YENİ
    private readonly ILogger<UserController> _logger; // 👈 YENİ

    // 👈 Constructor'ı (yapıcı metot) enjeksiyon için güncelle
    public UserController(IUserRepository userRepository, ILogger<UserController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    // 👈 Mevcut GetUserEmail metodunu "profile" olarak güncelleyelim
    [HttpGet("profile")]
    public async Task<IActionResult> GetUserProfile()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr))
        {
            return Unauthorized(new { error = "Geçersiz token." });
        }

        try
        {
            var user = await _userRepository.GetUserByIdAsync(Convert.ToInt32(userIdStr));
            if (user == null)
            {
                return NotFound(new { error = "Kullanıcı bulunamadı." });
            }

            // Sadece gerekli bilgileri döndür
            return Ok(new
            {
                user.Username,
                user.Email,
                user.Balance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı profili getirilirken hata oluştu. UserId: {UserId}", userIdStr);
            return StatusCode(500, new { error = "Sunucu hatası." });
        }
    }
}