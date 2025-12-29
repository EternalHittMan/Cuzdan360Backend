namespace Cuzdan360Backend.Services;

public static class EmailTemplates
{
    private static string GetBaseTemplate(string title, string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f4; padding: 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);"">
                        <td style=""padding: 40px 20px; text-align: center;"">
                            <h1 style=""color: #ffffff; margin: 0; font-size: 28px;"">Cüzdan360</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            {content}
                        </td>
                    </tr>
                    <tr style=""background-color: #f8f9fa;"">
                        <td style=""padding: 20px 30px; text-align: center; color: #6c757d; font-size: 12px;"">
                            <p style=""margin: 0;"">© 2025 Cüzdan360. Tüm hakları saklıdır.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string EmailVerification(string verificationLink, string username)
    {
        var content = $@"
            <h2 style=""color: #333; margin-top: 0;"">Merhaba {username}!</h2>
            <p style=""color: #666; line-height: 1.6;"">Cüzdan360'a hoş geldiniz! E-posta adresinizi doğrulamak için aşağıdaki butona tıklayın:</p>
            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""{verificationLink}"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #ffffff; padding: 14px 40px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold;"">E-postamı Doğrula</a>
            </div>
            <p style=""color: #666; line-height: 1.6; font-size: 14px;"">Veya aşağıdaki linki tarayıcınıza kopyalayın:</p>
            <p style=""color: #667eea; word-break: break-all; font-size: 12px;"">{verificationLink}</p>
            <p style=""color: #999; font-size: 12px; margin-top: 30px;"">Bu link 24 saat geçerlidir. Eğer bu işlemi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>";
        
        return GetBaseTemplate("E-posta Doğrulama", content);
    }

    public static string PasswordReset(string resetLink, string username)
    {
        var content = $@"
            <h2 style=""color: #333; margin-top: 0;"">Merhaba {username}!</h2>
            <p style=""color: #666; line-height: 1.6;"">Şifrenizi sıfırlamak için bir talepte bulundunuz. Aşağıdaki butona tıklayarak yeni şifrenizi oluşturabilirsiniz:</p>
            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""{resetLink}"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #ffffff; padding: 14px 40px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold;"">Şifremi Sıfırla</a>
            </div>
            <p style=""color: #666; line-height: 1.6; font-size: 14px;"">Veya aşağıdaki linki tarayıcınıza kopyalayın:</p>
            <p style=""color: #667eea; word-break: break-all; font-size: 12px;"">{resetLink}</p>
            <p style=""color: #dc3545; font-size: 14px; margin-top: 30px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #dc3545;"">
                <strong>Güvenlik Uyarısı:</strong> Eğer bu işlemi siz yapmadıysanız, lütfen derhal şifrenizi değiştirin ve bizimle iletişime geçin.
            </p>
            <p style=""color: #999; font-size: 12px;"">Bu link 1 saat geçerlidir.</p>";
        
        return GetBaseTemplate("Şifre Sıfırlama", content);
    }

    public static string OtpCode(string otpCode, string username)
    {
        var content = $@"
            <h2 style=""color: #333; margin-top: 0;"">Merhaba {username}!</h2>
            <p style=""color: #666; line-height: 1.6;"">Giriş yapmak için OTP kodunuz:</p>
            <div style=""text-align: center; margin: 30px 0;"">
                <div style=""background-color: #f8f9fa; border: 2px dashed #667eea; border-radius: 8px; padding: 20px; display: inline-block;"">
                    <span style=""font-size: 32px; font-weight: bold; color: #667eea; letter-spacing: 8px;"">{otpCode}</span>
                </div>
            </div>
            <p style=""color: #999; font-size: 12px; margin-top: 30px;"">Bu kod 5 dakika geçerlidir.</p>";
        
        return GetBaseTemplate("Giriş Kodu", content);
    }

    public static string PasswordChanged(string username)
    {
        var content = $@"
            <h2 style=""color: #333; margin-top: 0;"">Merhaba {username}!</h2>
            <p style=""color: #666; line-height: 1.6;"">Şifreniz başarıyla değiştirildi.</p>
            <p style=""color: #dc3545; font-size: 14px; margin-top: 30px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #dc3545;"">
                <strong>Güvenlik Uyarısı:</strong> Eğer bu işlemi siz yapmadıysanız, lütfen derhal bizimle iletişime geçin.
            </p>";
        
        return GetBaseTemplate("Şifre Değişikliği", content);
    }

    public static string EmailVerified(string username)
    {
        var content = $@"
            <h2 style=""color: #333; margin-top: 0;"">Merhaba {username}!</h2>
            <p style=""color: #666; line-height: 1.6;"">E-posta adresiniz başarıyla doğrulandı. Artık tüm özellikleri kullanabilirsiniz!</p>
            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""http://localhost:9003/login"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #ffffff; padding: 14px 40px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold;"">Giriş Yap</a>
            </div>";
        
        return GetBaseTemplate("E-posta Doğrulandı", content);
    }

    public static string EmailChangeNotification(string username, string newEmail)
    {
        var content = $@"
            <h2 style=""color: #333; margin-top: 0;"">Merhaba {username}!</h2>
            <p style=""color: #666; line-height: 1.6;"">Hesabınızda e-posta değişikliği talebi alındı.</p>
            <p style=""color: #666; line-height: 1.6;""><strong>Yeni e-posta adresi:</strong> {newEmail}</p>
            <div style=""margin: 30px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #dc3545; border-radius: 4px;"">
                <p style=""margin: 0; color: #856404;""><strong>⚠️ Güvenlik Uyarısı</strong></p>
                <p style=""margin: 10px 0 0 0; color: #856404; font-size: 14px;"">
                    Eğer bu işlemi siz yapmadıysanız, lütfen derhal şifrenizi değiştirin ve bizimle iletişime geçin.
                </p>
            </div>
            <p style=""color: #666; font-size: 14px;"">
                Yeni e-posta adresi doğrulanana kadar eski e-posta adresiniz aktif kalacaktır.
            </p>";
        
        return GetBaseTemplate("E-posta Değişikliği Bildirimi", content);
    }
}
