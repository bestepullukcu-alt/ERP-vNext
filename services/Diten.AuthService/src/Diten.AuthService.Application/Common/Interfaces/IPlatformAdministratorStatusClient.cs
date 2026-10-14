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
    /// to the record before minting (<see cref="Diten.AuthService.Application.Common.Services.PlatformAdministratorRoleConvergence"/>; FIX5 item 12 — its own file).</para>
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
