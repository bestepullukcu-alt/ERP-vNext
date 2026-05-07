namespace Diten.AuthService.Infrastructure.Settings;

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    public int OtpLength { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public string HashSecret { get; set; } = string.Empty;
}
