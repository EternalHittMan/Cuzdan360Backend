namespace Cuzdan360Backend.Models.DTOs;

public class TotpSetupResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeImage { get; set; } = string.Empty; // Base64 encoded PNG
}

public class VerifyTotpRequest
{
    public string Code { get; set; } = string.Empty;
}
