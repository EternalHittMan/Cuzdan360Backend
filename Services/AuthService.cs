using Cuzdan360Backend.Models;
using Cuzdan360Backend.Models.DTOs;
using Cuzdan360Backend.Repositories;
using Cuzdan360Backend.Utilities;
using System;
using System.Threading.Tasks;
using Cuzdan360Backend.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Cuzdan360Backend.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly TokenService _tokenService;
    private readonly EmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        TokenService tokenService,
        EmailService emailService,
        ILogger<AuthService> logger,
        IMemoryCache cache,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    private string GetUserIdFromToken()
    {
        var userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Kullanıcı kimliği doğrulanamadı.");
        }

        return userId;
    }

    
        public async Task<LoginResponse> LoginWithEmailAsync(LoginWithEmailRequest request)
    {
        _logger.LogInformation("Login işlemi başlatıldı. mail: {Email}", request.Email);

        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            await AddFailedLoginAttempt(request.Email);
            _logger.LogWarning("Geçersiz kimlik bilgileri. mail: {Email}", request.Email);
            throw new CustomException("Geçersiz kimlik bilgileri.", 401);
        }

        if (!user.IsEmailVerified)
        {
            throw new CustomException("Lütfen önce email adresinizi doğrulayın.", 401);
        }

        // OTP ayarlarını kontrol et
        var otpType = _configuration.GetValue<int>("OtpSettings:Type");
        var requiresOtp = false;

        switch (otpType)
        {
            case 0: // OTP kapalı
                requiresOtp = false;
                break;
            case 1: // Kullanıcı tercihi
                requiresOtp = user.IsOtpEnabled;
                break;
            case 2: // Tamamen açık
                requiresOtp = true;
                break;
            default:
                requiresOtp = false;
                break;
        }

        if (requiresOtp)
        {
            // OTP gönder ve beklet
            var otpBytes = new byte[4];
            RandomNumberGenerator.Fill(otpBytes); // 👈 Kriptografik RNG
            var otp = BitConverter.ToString(otpBytes).Replace("-", "").Substring(0, 6);

            user.MfaCode = otp;
            user.MfaCodeExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userRepository.UpdateUserAsync(user);

            await _emailService.SendEmailAsync(user.Email, "OTP Kodu", $"Giriş için OTP kodunuz: {otp}");

            _logger.LogInformation("OTP gönderildi. mail: {Email}", request.Email);

            return new LoginResponse
            {
                Token = null,
                RequiresOtp = true
            };
        }

        // OTP gerekmiyorsa direkt token oluştur
        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı. Kullanıcı: {Username}", request.Email);

        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Email, user.Permission.ToString());

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.LastLoginDate = DateTime.UtcNow;

        await _userRepository.UpdateUserAsync(user);

        return new LoginResponse
        {
            Token = token,
            RequiresOtp = false
        };
    }

    
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        _logger.LogInformation("Login işlemi başlatıldı. Kullanıcı: {Username}", request.Username);

        var user = await _userRepository.GetUserByUsernameAsync(request.Username);
        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            await AddFailedLoginAttempt(request.Username);
            _logger.LogWarning("Geçersiz kimlik bilgileri. Kullanıcı: {Username}", request.Username);
            throw new CustomException("Geçersiz kimlik bilgileri.", 401);
        }

        if (!user.IsEmailVerified)
        {
            throw new CustomException("Lütfen önce email adresinizi doğrulayın.", 401);
        }

        // OTP ayarlarını kontrol et
        var otpType = _configuration.GetValue<int>("OtpSettings:Type");
        var requiresOtp = false;

        switch (otpType)
        {
            case 0: // OTP kapalı
                requiresOtp = false;
                break;
            case 1: // Kullanıcı tercihi
                requiresOtp = user.IsOtpEnabled;
                break;
            case 2: // Tamamen açık
                requiresOtp = true;
                break;
            default:
                requiresOtp = false;
                break;
        }

        if (requiresOtp)
        {
            // OTP gönder ve beklet
            var otpBytes = new byte[4];
            RandomNumberGenerator.Fill(otpBytes); // 👈 Kriptografik RNG
            var otp = BitConverter.ToString(otpBytes).Replace("-", "").Substring(0, 6);

            user.MfaCode = otp;
            user.MfaCodeExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userRepository.UpdateUserAsync(user);

            await _emailService.SendEmailAsync(user.Email, "OTP Kodu", $"Giriş için OTP kodunuz: {otp}");

            _logger.LogInformation("OTP gönderildi. Kullanıcı: {Username}", request.Username);

            return new LoginResponse
            {
                Token = null,
                RequiresOtp = true
            };
        }

        // OTP gerekmiyorsa direkt token oluştur
        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı. Kullanıcı: {Username}", request.Username);

        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Email, user.Permission.ToString());

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.LastLoginDate = DateTime.UtcNow;

        await _userRepository.UpdateUserAsync(user);

        return new LoginResponse
        {
            Token = token,
            RequiresOtp = false
        };
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        _logger.LogInformation("Yeni kullanıcı kaydı başlatıldı. Kullanıcı: {Username}", request.Username);

        if (await _userRepository.GetUserByUsernameAsync(request.Username) != null)
        {
            _logger.LogWarning("Kullanıcı adı zaten alınmış. Kullanıcı: {Username}", request.Username);
            throw new CustomException("Bu kullanıcı adı zaten alınmış.", 400);
        }

        if (await _userRepository.GetUserByEmailAsync(request.Email) != null)
        {
            _logger.LogWarning("E-posta adresi zaten kullanılıyor. E-posta: {Email}", request.Email);
            throw new CustomException("Bu e-posta adresi zaten kullanılıyor.", 400);
        }

        ValidatePassword(request.Password);

        var user = new User
        {
            Username = request.Username,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            LastPasswordChangeDate = DateTime.UtcNow,
            IsEmailVerified = false
        };

        await _userRepository.AddUserAsync(user);

        var emailVerificationToken = Guid.NewGuid().ToString();
        user.EmailVerificationToken = emailVerificationToken;
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddDays(1);

        await _userRepository.UpdateUserAsync(user);

        var verificationLink = $"http://localhost:5000/verify-email?token={emailVerificationToken}";
        await _emailService.SendEmailAsync(user.Email, "E-posta Doğrulama",
            $"E-posta adresinizi doğrulamak için bu linki kullanın: {verificationLink}");

        _logger.LogInformation("Yeni kullanıcı başarıyla kaydedildi. Kullanıcı: {Username}", request.Username);
    }


    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        _logger.LogInformation("Şifre sıfırlama işlemi başlatıldı. E-posta: {Email}", request.Email);

        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("E-posta adresiyle kayıtlı kullanıcı bulunamadı. E-posta: {Email}", request.Email);
            throw new CustomException("Bu e-posta adresiyle kayıtlı bir kullanıcı bulunamadı.", 404);
        }

        var resetToken = Guid.NewGuid().ToString();
        user.ResetToken = resetToken;
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _userRepository.UpdateUserAsync(user);

        var resetLink = $"https://yourapp.com/reset-password?token={resetToken}";
        await _emailService.SendEmailAsync(user.Email, "Şifre Sıfırlama",
            $"Şifrenizi sıfırlamak için bu linki kullanın: {resetLink}");

        _logger.LogInformation("Şifre sıfırlama linki gönderildi. E-posta: {Email}", request.Email);
    }

    private async Task AddFailedPasswordAttempt(int userId)
    {
        var cacheKey = $"failed_password_attempts_{userId}";
        var attempts = await _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return Task.FromResult(0);
        });

        attempts++;

        if (attempts >= 5)
        {
            throw new CustomException("Çok fazla başarısız deneme. Lütfen 30 dakika sonra tekrar deneyin.", 429);
        }

        _cache.Set(cacheKey, attempts, TimeSpan.FromMinutes(30));
    }

    private async Task AddFailedLoginAttempt(string username)
    {
        var cacheKey = $"failed_login_attempts_{username.ToLower()}";
        var attempts = await _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return Task.FromResult(0);
        });

        attempts++;

        if (attempts >= 5)
        {
            throw new CustomException("Çok fazla başarısız deneme. Lütfen 30 dakika sonra tekrar deneyin.", 429);
        }

        _cache.Set(cacheKey, attempts, TimeSpan.FromMinutes(30));
    }

    private void ValidatePassword(string password)
    {
        var validationErrors = new List<string>();

        if (password.Length < 10)
            validationErrors.Add("Şifre en az 10 karakter uzunluğunda olmalıdır.");

        if (!password.Any(char.IsUpper))
            validationErrors.Add("Şifre en az bir büyük harf içermelidir.");

        if (!password.Any(char.IsLower))
            validationErrors.Add("Şifre en az bir küçük harf içermelidir.");

        if (!password.Any(char.IsDigit))
            validationErrors.Add("Şifre en az bir rakam içermelidir.");

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            validationErrors.Add("Şifre en az bir özel karakter içermelidir.");

        if (validationErrors.Any())
        {
            throw new CustomException(
                string.Join(" ", validationErrors),
                400
            );
        }
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var userId = GetUserIdFromToken();
        _logger.LogInformation("Şifre değiştirme işlemi başlatıldı. Kullanıcı ID: {UserId}", userId);

        var user = await _userRepository.GetUserByIdAsync(Convert.ToInt32(userId));
        if (user == null)
        {
            _logger.LogWarning("Kullanıcı bulunamadı. Kullanıcı ID: {UserId}", userId);
            throw new CustomException("Kullanıcı bulunamadı.", 404);
        }

        if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            await AddFailedPasswordAttempt(Convert.ToInt32(userId));

            _logger.LogWarning("Mevcut şifre yanlış. Kullanıcı ID: {UserId}", userId);
            throw new UnauthorizedAccessException("Mevcut şifre yanlış.");
        }

        ValidatePassword(request.NewPassword);

        if (PasswordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            throw new CustomException("Yeni şifreniz eski şifrenizle aynı olamaz.", 400);
        }

        user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        user.LastPasswordChangeDate = DateTime.UtcNow;

        // Tüm aktif oturumları sonlandır
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;

        await _userRepository.UpdateUserAsync(user);

        await _emailService.SendEmailAsync(user.Email,
            "Şifre Değişikliği Bildirimi",
            "Şifreniz az önce değiştirildi. Bu işlemi siz yapmadıysanız, lütfen hemen bizimle iletişime geçin.");

        _logger.LogInformation("Şifre başarıyla değiştirildi. Kullanıcı ID: {UserId}", userId);
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var userId = GetUserIdFromToken();

        var user = await _userRepository.GetUserByIdAsync(Convert.ToInt32(userId))
                   ?? throw new CustomException("Kullanıcı bulunamadı.", 404);

        // Email değişikliği
        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userRepository.GetUserByEmailAsync(request.Email) != null)
                throw new CustomException("Bu email zaten kullanımda.", 400);

            var token = Guid.NewGuid().ToString();
            user.PendingEmail = request.Email;
            user.EmailVerificationToken = token;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);

            await Task.WhenAll(
                _emailService.SendEmailAsync(
                    request.Email,
                    "Email Doğrulama",
                    $"Doğrulama linki: https://yourapp.com/verify-email?token={token}"),
                _emailService.SendEmailAsync(
                    user.Email,
                    "Email Değişikliği Bildirimi",
                    "Email değişikliği talebi alındı. İşlemi siz yapmadıysanız bize ulaşın.")
            );
        }

        // Username değişikliği
        if (!string.Equals(user.Username, request.Username, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userRepository.GetUserByUsernameAsync(request.Username) != null)
                throw new CustomException("Bu kullanıcı adı zaten kullanımda.", 400);

            user.Username = request.Username;
        }

        user.LastProfileUpdateDate = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);
    }

    public async Task<string> RefreshTokenAsync(RefreshTokenRequest request)
    {
        _logger.LogInformation("Refresh token işlemi başlatıldı. Refresh Token: {RefreshToken}", request.RefreshToken);

        var user = await _userRepository.GetUserByRefreshTokenAsync(request.RefreshToken);
        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Geçersiz refresh token. Refresh Token: {RefreshToken}", request.RefreshToken);
            throw new UnauthorizedAccessException("Geçersiz refresh token.");
        }

        // Yeni token oluştur
        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Email, user.Permission.ToString());

        // Yeni refresh token oluştur ve kullanıcıya kaydet
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("Jwt:RefreshTokenExpireDays"));

        await _userRepository.UpdateUserAsync(user);

        _logger.LogInformation("Yeni token başarıyla oluşturuldu. Kullanıcı ID: {UserId}", user.Id);

        return token;
    }

    public async Task<string> VerifyMfaAsync(VerifyMfaRequest request)
    {
        _logger.LogInformation("MFA doğrulama işlemi başlatıldı. E-posta: {Email}", request.Email);

        var user = await _userRepository.GetUserByMfaCodeAsync(request.Email, request.Otp);
        if (user == null || user.MfaCodeExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Geçersiz veya süresi dolmuş OTP. E-posta: {Email}", request.Email);
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş OTP.");
        }

        // OTP doğrulandı, yeni token oluştur
        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Email, user.Permission.ToString());

        // Refresh token oluştur ve kullanıcıya kaydet
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.MfaCode = null;
        user.MfaCodeExpiry = null;
        user.LastLoginDate = DateTime.UtcNow;

        await _userRepository.UpdateUserAsync(user);

        _logger.LogInformation("MFA başarıyla doğrulandı. Kullanıcı ID: {UserId}", user.Id);

        return token;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        _logger.LogInformation("Şifre sıfırlama işlemi başlatıldı. Token: {Token}", request.Token);

        var user = await _userRepository.GetUserByResetTokenAsync(request.Token);
        if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Geçersiz veya süresi dolmuş token. Token: {Token}", request.Token);
            throw new CustomException("Geçersiz veya süresi dolmuş token.", 400);
        }

        user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;

        await _userRepository.UpdateUserAsync(user);

        _logger.LogInformation("Şifre başarıyla sıfırlandı. Kullanıcı ID: {UserId}", user.Id);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request)
    {
        _logger.LogInformation("E-posta doğrulama işlemi başlatıldı. Token: {Token}", request.Token);

        var user = await _userRepository.GetUserByEmailVerificationTokenAsync(request.Token);
        if (user == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Geçersiz veya süresi dolmuş token. Token: {Token}", request.Token);
            throw new CustomException("Geçersiz veya süresi dolmuş token.", 400);
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;
        user.Email = user.PendingEmail;
        user.PendingEmail = null;

        await _userRepository.UpdateUserAsync(user);

        // E-posta gönder
        await _emailService.SendEmailAsync(user.Email, "E-posta Doğrulama", "E-posta adresiniz başarıyla doğrulandı.");

        _logger.LogInformation("E-posta başarıyla doğrulandı. Kullanıcı ID: {UserId}", user.Id);
    }
}