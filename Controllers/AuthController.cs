using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cuzdan360Backend.Models;
using Cuzdan360Backend.Services;
using Cuzdan360Backend.Models.DTOs;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Security.Claims;
using Cuzdan360Backend.Exceptions;

namespace Cuzdan360Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // eternalhittman/cuzdan360backend/EternalHittMan-Cuzdan360Backend-d9f10b2e9e1857f66f4861cea7a43a81242f69ad/Controllers/AuthController.cs
    
        [HttpPost("login-email")] // YENİ ENDPOINT (login-email)
        [EnableRateLimiting("LoginLimit")]
        public async Task<IActionResult> LoginWithEmail(LoginWithEmailRequest request) // LoginWithEmailRequest DTO'sunu kullan
        {
            try
            {
                // Doğru servis metodunu (e-posta ile) çağır
                var loginResponse = await _authService.LoginWithEmailAsync(request);
    
                if (loginResponse.RequiresOtp)
                {
                    return Ok(new { 
                        requiresOtp = true, 
                        message = "Lütfen Authenticator uygulamanızdaki kodu girin.",
                        isEmailVerified = loginResponse.IsEmailVerified,
                        email = loginResponse.Email
                    });
                }
    
                return Ok(new { 
                    token = loginResponse.Token,
                    refreshToken = loginResponse.RefreshToken, // 👈
                    requiresOtp = loginResponse.RequiresOtp,
                    isEmailVerified = loginResponse.IsEmailVerified,
                    email = loginResponse.Email
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Login with email failed for user {Email}", request.Email);
                return Unauthorized(new { message = ex.Message });
            }
            catch (CustomException ex) // Diğer özel hataları da yakala
            {
                _logger.LogWarning(ex, "Login with email failed for user {Email}", request.Email);
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }
    
    
    [HttpPost("login")]
    [EnableRateLimiting("LoginLimit")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        try
        {
            var loginResponse = await _authService.LoginAsync(request);

            if (loginResponse.RequiresOtp)
            {
                return Ok(new { requiresOtp = true, message = "OTP kodunuz e-posta adresinize gönderildi." });
            }

            return Ok(new { token = loginResponse.Token, refreshToken = loginResponse.RefreshToken }); // 👈
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Login failed for user {Username}", request.Username);
            return Unauthorized(new { message = ex.Message });
        }
    }


    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        try
        {
            await _authService.RegisterAsync(request);
            return Ok(new { message = "Kullanıcı başarıyla kaydedildi." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Registration failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Invalid token detected in ChangePassword");
                return Unauthorized(new { message = "Geçersiz token." });
            }

            await _authService.ChangePasswordAsync(request);
            return Ok(new { message = "Şifre başarıyla değiştirildi." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt in ChangePassword");
            return Unauthorized(new { message = ex.Message });
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, "Error in ChangePassword");
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("ForgotPasswordLimit")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        try
        {
            await _authService.ForgotPasswordAsync(request);
            return Ok(new { message = "Şifre sıfırlama linki e-posta adresinize gönderildi." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "ForgotPassword failed for email {Email}", request.Email);
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        try
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new { message = "Şifre başarıyla sıfırlandı." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "ResetPassword failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        try
        {
            await _authService.VerifyEmailAsync(request);
            return Ok(new { message = "Email adresiniz başarıyla doğrulandı." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Email verification failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("resend-verification-email")]
    public async Task<IActionResult> ResendVerificationEmail(ResendVerificationEmailRequest request)
    {
        try
        {
            await _authService.ResendVerificationEmailAsync(request);
            return Ok(new { message = "Doğrulama email'i tekrar gönderildi." });
        }
        catch (CustomException ex)
        {
            _logger.LogWarning(ex, "Resend verification email failed");
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("verify-mfa")]
    [EnableRateLimiting("OTPLimit")]
    public async Task<IActionResult> VerifyMfa(VerifyMfaRequest request)
    {
        try
        {
            var loginResponse = await _authService.VerifyMfaAsync(request);
            return Ok(new { 
                token = loginResponse.Token,
                refreshToken = loginResponse.RefreshToken, // 👈
                email = loginResponse.Email
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "MFA verification failed for email {Email}", request.Email);
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Invalid token detected in UpdateProfile");
                return Unauthorized(new { message = "Geçersiz token." });
            }

            var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            await _authService.UpdateProfileAsync(request);
            return Ok(new { message = "Profil bilgileri güncellendi." });
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, "Error in UpdateProfile");
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request);
            return Ok(new { 
                token = response.Token,
                refreshToken = response.RefreshToken // 👈
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "RefreshToken failed");
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request)
    {
        try
        {
            var loginResponse = await _authService.LoginWithGoogleAsync(request);
            return Ok(new { token = loginResponse.Token, refreshToken = loginResponse.RefreshToken }); // 👈
        }
        catch (CustomException ex)
        {
            _logger.LogWarning(ex, "Google login failed");
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google login failed");
            return StatusCode(500, new { message = "Google ile giriş başarısız" });
        }
    }


    // === TOTP ENDPOINTS ===

    [Authorize]
    [HttpPost("totp/enable")]
    public async Task<IActionResult> EnableTotp()
    {
        try
        {
            var result = await _authService.EnableTotpAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TOTP enable failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("totp/verify-and-activate")]
    public async Task<IActionResult> VerifyAndActivateTotp(VerifyTotpRequest request)
    {
        try
        {
            await _authService.VerifyAndActivateTotpAsync(request);
            return Ok(new { message = "2FA başarıyla etkinleştirildi." });
        }
        catch (CustomException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TOTP activation failed");
            return StatusCode(500, new { message = "İşlem başarısız." });
        }
    }

    [Authorize]
    [HttpPost("totp/disable")]
    public async Task<IActionResult> DisableTotp()
    {
         try
        {
            await _authService.DisableTotpAsync();
            return Ok(new { message = "2FA devre dışı bırakıldı." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TOTP disable failed");
            return StatusCode(500, new { message = "İşlem başarısız." });
        }
    }
}