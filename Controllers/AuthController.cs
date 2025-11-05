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

            return Ok(new { token = loginResponse.Token });
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

    [HttpPost("verify-mfa")]
    [EnableRateLimiting("OTPLimit")]
    public async Task<IActionResult> VerifyMfa(VerifyMfaRequest request)
    {
        try
        {
            var token = await _authService.VerifyMfaAsync(request);
            return Ok(new { token });
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
            var token = await _authService.RefreshTokenAsync(request);
            return Ok(new { token });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "RefreshToken failed");
            return Unauthorized(new { message = ex.Message });
        }
    }
}