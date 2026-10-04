using System.Net;
using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Contracts;

public interface IPlatformAdministratorProvisioningService
{
    Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct);
    Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct);
}

/// <param name="Status">BL-521 FIX5 item 11 — the record's status (its enum name): Auth switches an EXISTING account on only
/// for Active (as the sync does) and never confirms an existing account's e-mail; a missing status (an older caller) leaves
/// the existing account's state alone. A new account is created active, as before.</param>
public sealed record PlatformAdministratorProvisioningRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    bool RequirePasswordChange,
    string? Status = null)
{
    /// <summary>FIX5 item 6 — the administrator the call is about, for the log (identity only, never the address). Not part
    /// of the wire body.</summary>
    [JsonIgnore]
    public Guid AdministratorId { get; init; }
}

/// <param name="Email">The administrator's address — only the key Auth finds the account by. BL-521 FIX4 item 1: an
/// administrator's e-mail never changes, so Auth never moves an account to another address (FIX3's PreviousEmail is gone).</param>
/// <param name="Status">BL-521 FIX4 item 5 — the record's status (its enum name): Auth switches the account on only for
/// Active and otherwise leaves the account's own state alone.</param>
public sealed record PlatformAdministratorProvisioningSyncRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    string Status)
{
    /// <summary>FIX5 item 6 — the administrator the call is about, for the log (identity only, never the address). Not part
    /// of the wire body.</summary>
    [JsonIgnore]
    public Guid AdministratorId { get; init; }
}

public sealed record PlatformAdministratorProvisioningResult(
    string? SetupUrl,
    bool EmailSent);

/// <summary>
/// BL-521 FIX5 items 6 and 10 — WHAT A REFUSED OR FAILED CALL TO AUTH SAYS: a status code and a reason code derived from it,
/// NEVER Auth's reply. The old exception carried Auth's own message (and the old log line its whole body); both reach
/// Platform's logs and the caller's problem+json, and a validation or conflict reply can name the address or other
/// personal data. <see cref="Unavailable"/> tells the caller whether Auth GAVE NO ANSWER (a 5xx, a timeout) — the update
/// path then makes no second call to the same Auth (see <see cref="IsNoAnswer"/>).
/// </summary>
public sealed class PlatformAdministratorAuthCallException : InvalidOperationException
{
    public const string RequestRejected = "PLATFORM_ADMIN_AUTH_REQUEST_REJECTED";
    public const string InternalKeyRefused = "PLATFORM_ADMIN_AUTH_INTERNAL_KEY_REFUSED";
    public const string AccountNotFound = "PLATFORM_ADMIN_AUTH_ACCOUNT_NOT_FOUND";
    public const string Conflict = "PLATFORM_ADMIN_AUTH_CONFLICT";
    public const string AuthUnavailable = "PLATFORM_ADMIN_AUTH_UNAVAILABLE";
    public const string UnexpectedStatus = "PLATFORM_ADMIN_AUTH_UNEXPECTED_STATUS";

    public PlatformAdministratorAuthCallException(HttpStatusCode statusCode)
        : this((int)statusCode, ReasonCodeOf(statusCode))
    {
    }

    private PlatformAdministratorAuthCallException(int statusCode, string reasonCode)
        : base($"Platform administrator user provisioning failed ({reasonCode}).")
    {
        StatusCode = statusCode;
        ReasonCode = reasonCode;
    }

    /// <summary>The status code Auth answered with.</summary>
    public int StatusCode { get; }

    /// <summary>The stable code for that status (never taken from Auth's body).</summary>
    public string ReasonCode { get; }

    /// <summary>Auth gave no answer about the change: a 5xx, a request timeout, too many requests.</summary>
    public bool Unavailable => StatusCode >= 500 || StatusCode is 408 or 429;

    public static string ReasonCodeOf(HttpStatusCode statusCode) => (int)statusCode switch
    {
        400 or 422 => RequestRejected,
        401 or 403 => InternalKeyRefused,
        404 => AccountNotFound,
        409 => Conflict,
        >= 500 or 408 or 429 => AuthUnavailable,
        _ => UnexpectedStatus
    };

    /// <summary>
    /// Whether <paramref name="exception"/>, thrown by a call to Auth, means Auth GAVE NO ANSWER: its own 5xx / timeout
    /// reply, a connection failure, or the HttpClient's timeout (a cancellation the request did not ask for). A caller that
    /// cancelled its own request is not "no answer" — Auth may well be reachable.
    /// </summary>
    public static bool IsNoAnswer(Exception exception, CancellationToken requestToken) => exception switch
    {
        PlatformAdministratorAuthCallException call => call.Unavailable,
        HttpRequestException => true,
        TimeoutException => true,
        OperationCanceledException => !requestToken.IsCancellationRequested,
        _ => false
    };
}
