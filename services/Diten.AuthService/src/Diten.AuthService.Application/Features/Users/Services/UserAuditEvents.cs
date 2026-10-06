namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// BL-456 — the user-lifecycle audit vocabulary: the <c>authAuditLogs.EventName</c> each user mutation writes, and the
/// Platform <c>AuditOperation</c> it is filed under in the central log (category IdentityAccess, entity "User").
///
/// <para>The event name travels to Platform as the audit event's <c>RequestType</c>; the Platform Audit Log screen labels
/// it from its own resx (<c>AuditLog.Event.{name}</c>, en + tr). <c>UserAuditEventLabelGuardTests</c> holds this list and
/// those labels equal — an event added here without a label is red.</para>
///
/// <para>The integers mirror <c>Diten.Platform.Domain.Enums.AuditOperation</c> / <c>AuditOutcome</c> (AuthService does not
/// reference the Platform domain; MDM's forwarder mirrors them the same way).</para>
/// </summary>
public static class UserAuditEvents
{
    public const string Invited = "user_invited";
    /// <summary>The self-service create (an administrator typed the password) — the other door of CreateUser.</summary>
    public const string Created = "user_created";
    public const string Deleted = "user_deleted";
    public const string Deactivated = "user_deactivated";
    public const string Activated = "user_activated";
    public const string PasswordResetByAdmin = "user_password_reset_by_admin";
    public const string InvitationResent = "user_invitation_resent";
    public const string Updated = "user_updated";
    public const string RoleAssigned = "user_role_assigned";
    public const string RoleRemoved = "user_role_removed";
    public const string AccountKindChanged = AccountKindWriter.AuditEventName;

    public const string EntityType = "User";

    // Platform AuditOperation values.
    private const int OperationCreate = 1;
    private const int OperationUpdate = 2;
    private const int OperationDelete = 3;
    private const int OperationActivate = 4;
    private const int OperationDeactivate = 5;
    private const int OperationAssign = 8;
    private const int OperationRevoke = 9;
    private const int OperationExecute = 15;

    /// <summary>Platform AuditOutcome.Succeeded — only completed mutations are audited here.</summary>
    public const int OutcomeSucceeded = 1;

    /// <summary>Platform AuditOutcome.Failed — BL-529: a reset that conflicted or failed after it ended sessions.</summary>
    public const int OutcomeFailed = 2;

    /// <summary>Every user-lifecycle event and the Platform operation it is filed under. The forwarder refuses nothing else.</summary>
    public static readonly IReadOnlyDictionary<string, int> Operations = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Invited] = OperationCreate,
        [Created] = OperationCreate,
        [Deleted] = OperationDelete,
        [Deactivated] = OperationDeactivate,
        [Activated] = OperationActivate,
        [PasswordResetByAdmin] = OperationExecute,
        [InvitationResent] = OperationExecute,
        [Updated] = OperationUpdate,
        [RoleAssigned] = OperationAssign,
        [RoleRemoved] = OperationRevoke,
        [AccountKindChanged] = OperationUpdate
    };
}
