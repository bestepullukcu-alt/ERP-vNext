namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// MOD-0022 E1 — a short-lived, single-use proof that a tenant user re-entered their password (or, in KS-3 window mode,
/// did so within the policy window) for one e-signature purpose. Platform consumes it once through the internal API.
///
/// <para>The plain ticket is returned to the caller exactly once and is NEVER stored: only its keyed hash
/// (<see cref="TicketHash"/>). The ticket opens no session and touches no refresh token.</para>
/// </summary>
public sealed class ESignReauthTicket : EntityBase
{
    public const string PurposeESign = "esign";

    private ESignReauthTicket() { }

    public ESignReauthTicket(
        Guid tenantId,
        Guid userId,
        string ticketHash,
        string intent,
        ESignReauthKind kind,
        DateTime passwordVerifiedAtUtc,
        DateTime expiresAtUtc,
        string createdIp,
        string userAgentHash)
    {
        TenantId = tenantId;
        UserId = userId;
        TicketHash = ticketHash;
        Purpose = PurposeESign;
        Intent = intent;
        Kind = kind;
        PasswordVerifiedAtUtc = passwordVerifiedAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
        CreatedIp = createdIp;
        UserAgentHash = userAgentHash;
    }

    public Guid UserId { get; private set; }
    public string TicketHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = PurposeESign;
    public string Intent { get; private set; } = string.Empty;
    public ESignReauthKind Kind { get; private set; }
    public DateTime PasswordVerifiedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public string? ConsumedBy { get; private set; }
    public string CreatedIp { get; private set; } = string.Empty;
    public string UserAgentHash { get; private set; } = string.Empty;
}

/// <summary>MOD-0022 E1 — the re-authentication behind a ticket.</summary>
public enum ESignReauthKind
{
    /// <summary>The password was entered for this ticket.</summary>
    Password = 0,

    /// <summary>KS-3 "once within a window": issued from an open window without a password.</summary>
    Window = 1,
}

/// <summary>MOD-0022 E1 — what the ticket may be used for (ESIGN-BUNDLE §4.1).</summary>
public static class ESignReauthIntents
{
    public const string Sign = "sign";
    public const string Sample = "sample";
    public const string Invalidate = "invalidate";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Sign, Sample, Invalidate };

    /// <summary>Intents a window ticket may NOT serve: saving a sample and invalidating always need the password.</summary>
    public static bool RequiresPassword(string intent) => intent is Sample or Invalidate;
}
