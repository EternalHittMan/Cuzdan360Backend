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
using Google.Apis.Auth;

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
    private readonly ITotpService _totpService;

    public AuthService(
        IUserRepository userRepository,
        TokenService tokenService,
        EmailService emailService,
        ILogger<AuthService> logger,
        IMemoryCache cache,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ITotpService totpService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _totpService = totpService;
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
            _logger.LogWarning("Email doğrulanmamış. Email: {Email}", request.Email);
            return new LoginResponse
            {
                Token = null,
                RequiresOtp = false,
                IsEmailVerified = false,
                Email = user.Email
            };
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

        // TOTP kontrolü (Email OTP kaldırıldı)
        if (user.TotpSecret != null)
        {
            return new LoginResponse
            {
                Token = null,
                RequiresOtp = true,
                IsEmailVerified = user.IsEmailVerified,
                Email = user.Email
            };
        }

        // OTP gerekmiyorsa (veya TOTP kurulu değilse) direkt token oluştur
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
            RefreshToken = refreshToken, // 👈
            RequiresOtp = false,
            IsEmailVerified = user.IsEmailVerified,
            Email = user.Email
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
            RefreshToken = refreshToken, // 👈
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
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(3);

        await _userRepository.UpdateUserAsync(user);

        var verificationLink = $"{_configuration["AppSettings:FrontendUrl"]}/email-confirmation?token={emailVerificationToken}";
        await _emailService.SendEmailAsync(
            user.Email,
            "E-posta Doğrulama",
            EmailTemplates.EmailVerification(verificationLink, user.Username)
        );

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

        var resetLink = $"{_configuration["AppSettings:FrontendUrl"]}/forgot-password?token={resetToken}";
        await _emailService.SendEmailAsync(
            user.Email,
            "Şifre Sıfırlama",
            EmailTemplates.PasswordReset(resetLink, user.Username)
        );

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
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(3); // 3 dakika

            var verificationLink = $"{_configuration["AppSettings:FrontendUrl"]}/email-confirmation?token={token}";
            
            await Task.WhenAll(
                _emailService.SendEmailAsync(
                    request.Email,
                    "E-posta Doğrulama",
                    EmailTemplates.EmailVerification(verificationLink, user.Username)
                ),
                _emailService.SendEmailAsync(
                    user.Email,
                    "E-posta Değişikliği Bildirimi",
                    EmailTemplates.EmailChangeNotification(user.Username, request.Email)
                )
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

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request) // 👈 Dönüş tipi değişti
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

        return new LoginResponse // 👈 String yerine LoginResponse
        { 
            Token = token, 
            RefreshToken = newRefreshToken 
        };
    }

    public async Task<LoginResponse> VerifyMfaAsync(VerifyMfaRequest request) // 👈 Dönüş tipi değişti
    {
        _logger.LogInformation("MFA doğrulama işlemi başlatıldı. E-posta: {Email}", request.Email);

        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
             throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        }

        // TOTP kontrolü
        if (user.TotpSecret == null)
        {
             throw new UnauthorizedAccessException("2FA kurulu değil.");
        }

        if (!_totpService.ValidateCode(user.TotpSecret, request.Otp))
        {
            _logger.LogWarning("Geçersiz TOTP kodu. E-posta: {Email}", request.Email);
            throw new UnauthorizedAccessException("Geçersiz kod.");
        }

        // OTP doğrulandı, yeni token oluştur
        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Email, user.Permission.ToString());

        // Refresh token oluştur ve kullanıcıya kaydet
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("Jwt:RefreshTokenExpireDays"));
        // Email OTP alanlarını temizlemeye gerek yok artık kullanılmıyor
        user.LastLoginDate = DateTime.UtcNow;

        await _userRepository.UpdateUserAsync(user);

        _logger.LogInformation("MFA başarıyla doğrulandı. Kullanıcı ID: {UserId}", user.Id);

        return new LoginResponse
        {
             Token = token,
             RefreshToken = refreshToken,
             RequiresOtp = false,
             IsEmailVerified = user.IsEmailVerified,
             Email = user.Email
        };
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
        
        // PendingEmail varsa (email değişikliği durumu), Email'i güncelle
        if (!string.IsNullOrEmpty(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
        }

        await _userRepository.UpdateUserAsync(user);

        // E-posta gönder
        await _emailService.SendEmailAsync(
            user.Email,
            "E-posta Doğrulandı",
            EmailTemplates.EmailVerified(user.Username)
        );

        _logger.LogInformation("E-posta başarıyla doğrulandı. Kullanıcı ID: {UserId}", user.Id);
    }

    public async Task ResendVerificationEmailAsync(ResendVerificationEmailRequest request)
    {
        _logger.LogInformation("Email doğrulama tekrar gönderme işlemi başlatıldı. Email: {Email}", request.Email);

        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Kullanıcı bulunamadı. Email: {Email}", request.Email);
            throw new CustomException("Kullanıcı bulunamadı", 404);
        }

        if (user.IsEmailVerified)
        {
            _logger.LogWarning("Email zaten doğrulanmış. Email: {Email}", request.Email);
            throw new CustomException("Email zaten doğrulanmış", 400);
        }

        // Yeni token oluştur
        var emailVerificationToken = Guid.NewGuid().ToString();
        user.EmailVerificationToken = emailVerificationToken;
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(3);

        await _userRepository.UpdateUserAsync(user);

        var verificationLink = $"{_configuration["AppSettings:FrontendUrl"]}/email-confirmation?token={emailVerificationToken}";
        await _emailService.SendEmailAsync(
            user.Email,
            "E-posta Doğrulama",
            EmailTemplates.EmailVerification(verificationLink, user.Username)
        );

        _logger.LogInformation("Doğrulama email'i tekrar gönderildi. Email: {Email}", request.Email);
    }

    public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest request)
    {
        try
        {
            _logger.LogInformation("Google ile giriş işlemi başlatıldı.");

            // Google ID token'ı doğrula
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

            _logger.LogInformation("Google token doğrulandı. Email: {Email}", payload.Email);

            // Email ile kullanıcıyı ara
            var user = await _userRepository.GetUserByEmailAsync(payload.Email);

            if (user == null)
            {
                // Yeni kullanıcı oluştur
                var username = payload.Email.Split('@')[0] + "_" + Guid.NewGuid().ToString().Substring(0, 4);
                
                user = new User
                {
                    Username = username,
                    Email = payload.Email,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    PasswordHash = string.Empty, // Google ile giriş yapan kullanıcılar için şifre yok
                    LastPasswordChangeDate = DateTime.UtcNow
                };
                
                await _userRepository.AddUserAsync(user);

                _logger.LogInformation("Yeni Google kullanıcısı oluşturuldu. Email: {Email}, Username: {Username}", payload.Email, username);
            }

            // JWT token oluştur ve döndür
            var token = _tokenService.GenerateToken(user.Id.ToString(), user.Email, user.Permission.ToString());

            // Refresh token oluştur
            var refreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            user.LastLoginDate = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);

            _logger.LogInformation("Google ile giriş başarılı. Email: {Email}", payload.Email);

            return new LoginResponse
            {
                Token = token,
                RefreshToken = refreshToken, // 👈
                RequiresOtp = false
            };
        }
        catch (Google.Apis.Auth.InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Geçersiz Google token");
            throw new CustomException("Geçersiz Google token", 401);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google ile giriş sırasında hata oluştu");
            throw new CustomException("Google ile giriş başarısız", 500);
        }
    }

    // === TOTP YENİ METODLAR ===

    public async Task<TotpSetupResponse> EnableTotpAsync()
    {
        var userId = GetUserIdFromToken();
        var user = await _userRepository.GetUserByIdAsync(Convert.ToInt32(userId));

        if (user == null)
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var secret = _totpService.GenerateSecret();
        var qrCodeUri = _totpService.GenerateQrCodeUri(user.Email, secret);
        var qrCodeBytes = _totpService.GenerateQrCodeImage(qrCodeUri);
        var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

        // Secret'ı geçici olarak cache'te sakla (10 dakika)
        // Kullanıcı doğrulayana kadar DB'ye kaydetme veya 'Pending' bir alanda tut.
        // Cache kullanmak stateless backend için daha temiz (redis vs varsayarsak, burada in-memory)
        var cacheKey = $"pending_totp_{userId}";
        _cache.Set(cacheKey, secret, TimeSpan.FromMinutes(10));

        return new TotpSetupResponse
        {
            Secret = secret,
            QrCodeImage = $"data:image/png;base64,{qrCodeBase64}"
        };
    }

    public async Task VerifyAndActivateTotpAsync(VerifyTotpRequest request)
    {
        var userId = GetUserIdFromToken();
        
        // Önce cache'ten pending secret'ı al
        var cacheKey = $"pending_totp_{userId}";
        if (!_cache.TryGetValue<string>(cacheKey, out var secret))
        {
             throw new CustomException("Kurulum süresi dolmuş veya geçersiz işlem. Lütfen tekrar kurulum yapın.", 400);
        }

        // Kodu doğrula
        var isValid = _totpService.ValidateCode(secret, request.Code);
        if (!isValid)
        {
            throw new CustomException("Geçersiz kod.", 400);
        }

        // Valid -> Kullanıcıya kaydet
        var user = await _userRepository.GetUserByIdAsync(Convert.ToInt32(userId));
        if (user == null) throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        user.TotpSecret = secret;
        user.IsOtpEnabled = true;

        await _userRepository.UpdateUserAsync(user);

        // Cache'i temizle
        _cache.Remove(cacheKey);

        _logger.LogInformation("TOTP başarıyla kuruldu. Kullanıcı ID: {UserId}", userId);
    }

    public async Task DisableTotpAsync()
    {
        var userId = GetUserIdFromToken();
        var user = await _userRepository.GetUserByIdAsync(Convert.ToInt32(userId));
        
        if (user == null) throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        user.TotpSecret = null;
        user.IsOtpEnabled = false;

        await _userRepository.UpdateUserAsync(user);

        _logger.LogInformation("TOTP devre dışı bırakıldı. Kullanıcı ID: {UserId}", userId);
    }
}