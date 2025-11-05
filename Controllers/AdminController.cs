using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models;
using Cuzdan360Backend.Models.DTOs;
using Cuzdan360Backend.Utilities;
using Microsoft.Extensions.Logging;

namespace Cuzdan360Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }


    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _context.Users.ToListAsync();
            return Ok(new { data = users });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
        }
    }


    [HttpPost("user/create")]
    public async Task<IActionResult> CreateUser(RegisterRequest request)
    {
        try
        {
            var user = new User
            {
                Username = request.Username,
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                LastPasswordChangeDate = DateTime.UtcNow,
                IsEmailVerified = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı başarıyla oluşturuldu.", data = user });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
        }
    }

    [HttpDelete("user/delete/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { error = "Kullanıcı bulunamadı." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı başarıyla silindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
        }
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { error = "Kullanıcı bulunamadı." });
            }

            return Ok(new { data = user });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
        }
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, UserUpdateDto userDto)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { error = "Kullanıcı bulunamadı." });
            }

            // Update user properties
            user.Username = userDto.Username ?? user.Username;
            user.Email = userDto.Email ?? user.Email;
            user.Balance = userDto.Balance ?? user.Balance;
            user.Permission = userDto.Permission ?? user.Permission;
            user.IsEmailVerified = userDto.IsEmailVerified ?? user.IsEmailVerified;
            user.IsOtpEnabled = userDto.IsOtpEnabled ?? user.IsOtpEnabled;

            // Optional fields update
            if (userDto.ResetToken != null)
            {
                user.ResetToken = userDto.ResetToken;
                user.ResetTokenExpiry = userDto.ResetTokenExpiry;
            }

            if (userDto.EmailVerificationToken != null)
            {
                user.EmailVerificationToken = userDto.EmailVerificationToken;
                user.EmailVerificationTokenExpiry = userDto.EmailVerificationTokenExpiry;
            }

            if (userDto.MfaCode != null)
            {
                user.MfaCode = userDto.MfaCode;
                user.MfaCodeExpiry = userDto.MfaCodeExpiry;
            }

            if (userDto.PendingEmail != null)
            {
                user.PendingEmail = userDto.PendingEmail;
            }

            if (userDto.RefreshToken != null)
            {
                user.RefreshToken = userDto.RefreshToken;
                user.RefreshTokenExpiry = userDto.RefreshTokenExpiry;
            }

            // Update timestamps
            if (userDto.UpdateLastProfileUpdate)
            {
                user.LastProfileUpdateDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Kullanıcı başarıyla güncellendi." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating user {UserId}", id);
            return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
        }
    }
}

// DTO for user updates
public class UserUpdateDto
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public int? Balance { get; set; }
    public int? Permission { get; set; }
    public bool? IsEmailVerified { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }
    public string? MfaCode { get; set; }
    public DateTime? MfaCodeExpiry { get; set; }
    public string? PendingEmail { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public bool? IsOtpEnabled { get; set; }
    public bool UpdateLastProfileUpdate { get; set; } = false;
}