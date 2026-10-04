using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPlatformAdministratorStatusClient
{
    /// <summary>
    /// The administrator's Platform record is active. Fail closed: no answer (Platform unreachable, an unreadable reply)
    /// is "not active" here. Used by the password-reset doors, which mint no token; every door that MINTS a platform token
    /// asks <see cref="GetActiveActorTypeAsync"/> instead.
    /// </summary>
    Task<bool> IsActiveAsync(string email, CancellationToken ct);

    Task MarkLoginAcceptedAsync(string email, CancellationToken ct);

    /// <summary>
    /// BL-521 — THE ACTOR TYPE A PLATFORM TOKEN IS MINTED WITH. Every door that issues a platform token (login, forced
    /// password change, refresh) takes the actor type from the administrator's Platform RECORD through this call, never
    /// from the old token or from Auth's own copy on the user (<c>User.PlatformActorType</c>, best-effort and possibly
    /// stale): a partner administrator cannot mint itself a <c>platform_admin</c> token, and a demoted administrator does
    /// not keep one (FIX1 F1(b), FIX2 item 1).
    ///
    /// <para>FIX2 item 3 — three answers, not two, because "Platform said no" and "Platform did not answer" must not be
    /// treated alike: a definitive no ends the administrator's sessions, a missing answer only refuses this one request
    /// (503) so the user can retry. See <see cref="PlatformAdministratorRecordVerdict"/>.</para>
    ///
    /// <para>FIX4 item 2 — a confirmed answer also carries the record's roles, so every door converges Auth's copy of them
    /// to the record before minting (<see cref="PlatformAdministratorRoleConvergence"/>).</para>
    /// </summary>
    Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct);
}

/// <summary>BL-521 FIX2 item 3 — what Platform answered about an administrator record.</summary>
public enum PlatformAdministratorRecordVerdict
{
    /// <summary>An active record of a known actor type; <see cref="PlatformAdministratorRecordAnswer.ActorType"/> names it.</summary>
    Confirmed = 1,

    /// <summary>
    /// Platform's definitive no: no such record (its own 404), a record that is not active (suspended, disabled,
    /// deleted), or an actor type this service does not know. The caller refuses and revokes the administrator's
    /// sessions.
    /// </summary>
    Refused = 2,

    /// <summary>
    /// No answer: Platform unreachable, a timeout, a 5xx (a deployment in progress), any other non-record reply, an
    /// unreadable body, or this service missing its internal key. The caller refuses THIS request with 503 and revokes
    /// NOTHING — the user retries.
    /// </summary>
    Unavailable = 3
}

/// <summary>BL-521 FIX2 item 3 — Platform's answer about an administrator record, with the token actor type when confirmed.</summary>
public sealed record PlatformAdministratorRecordAnswer
{
    /// <summary>The token actor type of a platform administrator.</summary>
    public const string PlatformAdminActorType = "platform_admin";

    /// <summary>The token actor type of a partner administrator.</summary>
    public const string PartnerAdminActorType = "partner_admin";

    /// <summary>
    /// The 503 sentence a door answers with when Platform gave no answer. Deliberately the SAME sentence the Web login
    /// already shows when it cannot reach this service (AuthGateway.AuthServiceUnavailableMessage): the login page shows
    /// Auth's error text as it comes, so a new sentence here would be new untranslated screen text. Which edge failed
    /// is in the log, not on the screen.
    /// </summary>
    public const string UnavailableMessage = "The authentication service is temporarily unavailable. Please try again in a moment.";

    private PlatformAdministratorRecordAnswer(PlatformAdministratorRecordVerdict verdict, string? actorType, IReadOnlyList<string>? roles)
    {
        Verdict = verdict;
        ActorType = actorType;
        Roles = roles;
    }

    public PlatformAdministratorRecordVerdict Verdict { get; }

    /// <summary><c>platform_admin</c> or <c>partner_admin</c> when <see cref="Verdict"/> is Confirmed; otherwise null.</summary>
    public string? ActorType { get; }

    /// <summary>
    /// BL-521 FIX4 item 2 — the record's roles (Platform's role names) when Confirmed and Platform sent them; null when an
    /// older Platform did not (nothing is converged then — the answer is still a valid one about the record).
    /// </summary>
    public IReadOnlyList<string>? Roles { get; }

    public static PlatformAdministratorRecordAnswer Refused { get; } = new(PlatformAdministratorRecordVerdict.Refused, null, null);

    public static PlatformAdministratorRecordAnswer Unavailable { get; } = new(PlatformAdministratorRecordVerdict.Unavailable, null, null);

    /// <summary>A confirmed record; only the two known token actor types can be confirmed.</summary>
    public static PlatformAdministratorRecordAnswer ConfirmedAs(string actorType, IReadOnlyList<string>? roles = null) =>
        actorType is PlatformAdminActorType or PartnerAdminActorType
            ? new PlatformAdministratorRecordAnswer(PlatformAdministratorRecordVerdict.Confirmed, actorType, roles)
            : throw new ArgumentException($"'{actorType}' is not a platform token actor type.", nameof(actorType));
}

/// <summary>
/// BL-521 FIX4 item 2 — AUTH'S COPY OF A PLATFORM ADMINISTRATOR'S ROLES FOLLOWS THE PLATFORM RECORD. Platform writes the
/// record and syncs Auth, but a sync can be lost (Platform's update syncs Auth before its own write; a write refused
/// afterwards converges Auth to the record — and that can fail too), and some record changes never sync at all. So every
/// door that mints a platform token (login, refresh, forced password change) converges the copy to the roles the record
/// names right before minting, and the sync uses the same rule. Only the four platform roles are touched; any other role
/// the account holds is left as it is.
/// </summary>
public static class PlatformAdministratorRoleConvergence
{
    /// <summary>The description a platform role is created with.</summary>
    public const string RoleDescription = "Platform administrator role";

    private static readonly string[] PlatformRoles = ["SuperAdmin", "BillingAdmin", "SupportAdmin", "ReadOnly"];

    /// <summary>One of the four platform roles (any letter case).</summary>
    public static bool IsPlatformRole(string role) => Canonical(role) is not null;

    /// <summary>
    /// The platform roles a record's role list stands for: known names only, canonical spelling, each once; none at all →
    /// ReadOnly (the rule the sync always had: a platform account never ends up without a platform role).
    /// </summary>
    public static IReadOnlyList<string> Desired(IEnumerable<string>? recordRoles)
    {
        var desired = (recordRoles ?? [])
            .Select(Canonical)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return desired.Count == 0 ? ["ReadOnly"] : desired;
    }

    /// <summary>
    /// Brings the account's platform roles to <see cref="Desired"/>: missing ones assigned, extra ones revoked; nothing is
    /// written when they already match. Returns whether anything changed.
    /// </summary>
    public static async Task<bool> ConvergeAsync(
        IUserRoleRepository userRoles,
        IRoleRepository roles,
        Guid userId,
        Guid tenantId,
        IEnumerable<string>? recordRoles,
        CancellationToken ct)
    {
        var desired = Desired(recordRoles);
        var current = (await userRoles.GetRolesByUserAsync(userId, tenantId, ct))
            .Select(Canonical)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var missing = desired.Except(current, StringComparer.Ordinal).ToList();
        var extra = current.Except(desired, StringComparer.Ordinal).ToList();
        if (missing.Count == 0 && extra.Count == 0)
        {
            return false;
        }

        foreach (var roleName in missing)
        {
            var role = await roles.UpsertSystemRoleAsync(roleName, roleName, RoleDescription, tenantId, ct);
            if (!await userRoles.ExistsAsync(userId, role.Id, tenantId, ct))
            {
                await userRoles.AssignAsync(new UserRole(userId, role.Id, tenantId, "system"), ct);
            }
        }

        foreach (var roleName in extra)
        {
            var role = await roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is not null)
            {
                await userRoles.RevokeAsync(userId, role.Id, tenantId, ct);
            }
        }

        return true;
    }

    private static string? Canonical(string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? null
            : PlatformRoles.FirstOrDefault(known => string.Equals(known, role.Trim(), StringComparison.OrdinalIgnoreCase));
}
