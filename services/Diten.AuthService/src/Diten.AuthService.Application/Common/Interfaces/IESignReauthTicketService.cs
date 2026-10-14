using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// MOD-0022 E1 — issues, consumes and revokes e-signature re-authentication tickets and KS-3 windows.
/// The plain ticket exists only in the <see cref="IssuedESignTicket"/> returned to the caller; storage holds its
/// keyed hash. Implementations must never log the ticket.
/// </summary>
public interface IESignReauthTicketService
{
    /// <summary>False when no hashing key is configured; callers then fail closed (503).</summary>
    bool IsConfigured { get; }

    Task<IssuedESignTicket> IssueAsync(
        Guid tenantId,
        Guid userId,
        string intent,
        ESignReauthKind kind,
        DateTime passwordVerifiedAtUtc,
        string requestIp,
        string? userAgent,
        CancellationToken ct);

    /// <summary>Opens (or replaces) the user's window for <paramref name="minutes"/> (1–60). Returns its expiry.</summary>
    Task<DateTime> OpenWindowAsync(Guid tenantId, Guid userId, DateTime passwordVerifiedAtUtc, int minutes, string? userAgent, CancellationToken ct);

    /// <summary>The open window of this user for this browser (same user-agent hash), or null.</summary>
    Task<ESignReauthWindow?> GetOpenWindowAsync(Guid tenantId, Guid userId, string? userAgent, CancellationToken ct);

    /// <summary>
    /// Atomic single use: marks the ticket consumed only if it exists, belongs to this tenant + user + intent, is not
    /// consumed and not expired — in ONE conditional update. Null for any miss (no oracle on which part failed).
    /// </summary>
    Task<ESignReauthTicket?> ConsumeAsync(string ticket, Guid tenantId, Guid userId, string intent, string consumedBy, CancellationToken ct);

    /// <summary>Revokes every open window of the user (password change, forced change, administrator set, logout).</summary>
    Task RevokeWindowsAsync(Guid tenantId, Guid userId, string reason, CancellationToken ct);
}

public sealed record IssuedESignTicket(string Ticket, DateTime ExpiresAtUtc)
{
    // Never print the ticket (records otherwise render every member).
    public override string ToString() => $"IssuedESignTicket {{ ExpiresAtUtc = {ExpiresAtUtc:O} }}";
}

public interface IESignReauthTicketRepository
{
    Task CreateAsync(ESignReauthTicket ticket, CancellationToken ct);

    Task<ESignReauthTicket?> TryConsumeAsync(
        string ticketHash,
        Guid tenantId,
        Guid userId,
        string intent,
        DateTime nowUtc,
        string consumedBy,
        CancellationToken ct);
}

public interface IESignReauthWindowRepository
{
    Task UpsertAsync(ESignReauthWindow window, CancellationToken ct);

    Task<ESignReauthWindow?> GetOpenAsync(Guid tenantId, Guid userId, DateTime nowUtc, CancellationToken ct);

    Task<long> RevokeAllAsync(Guid tenantId, Guid userId, string reason, DateTime nowUtc, CancellationToken ct);
}
