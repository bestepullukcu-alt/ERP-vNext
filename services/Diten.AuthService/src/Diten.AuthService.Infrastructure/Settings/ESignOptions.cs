namespace Diten.AuthService.Infrastructure.Settings;

/// <summary>MOD-0022 E1 — e-signature re-authentication tickets (section "ESign").</summary>
public sealed class ESignOptions
{
    public const string SectionName = "ESign";

    /// <summary>CT-D1: 120 s by default, clamped to 30–300.</summary>
    public int TicketLifetimeSeconds { get; set; } = 120;

    /// <summary>
    /// HMAC key for ticket hashes. When empty, the JWT signing secret is used with a purpose prefix — the same key the
    /// refresh-token hash already uses (RefreshTokenHasher). With neither, tickets are refused (fail closed, 503).
    /// </summary>
    public string HashSecret { get; set; } = string.Empty;
}
