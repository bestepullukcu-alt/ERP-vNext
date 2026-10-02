namespace Diten.AuthService.Application.Features.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 — the role audit vocabulary: the <c>authAuditLogs.EventName</c> each role mutation writes, and the
/// Platform <c>AuditOperation</c> it is filed under in the central log (category IdentityAccess, entity "Role", entity
/// id = the role). The role side of the bridge <c>UserAuditEvents</c> opened for users (BL-456): before it, "who deleted
/// this role / who granted this permission" was in the database and on no screen.
///
/// <para>The event name travels to Platform as the audit event's <c>RequestType</c>; the Platform Audit Log screen labels
/// it from its own resx (<c>AuditLog.Event.{name}</c>, en + tr). <c>UserAuditEventLabelGuardTests</c> holds the union of
/// this list and <c>UserAuditEvents</c> equal to those labels.</para>
///
/// <para>The integers mirror <c>Diten.Platform.Domain.Enums.AuditOperation</c> / <c>AuditOutcome</c>, as in
/// <c>UserAuditEvents</c> (AuthService does not reference the Platform domain).</para>
/// </summary>
public static class RoleAuditEvents
{
    public const string Created = "role_created";
    public const string Updated = "role_updated";
    public const string Deleted = "role_deleted";
    public const string PermissionGranted = "role_permission_granted";
    public const string PermissionRevoked = "role_permission_revoked";

    public const string EntityType = "Role";

    // Platform AuditOperation values.
    private const int OperationCreate = 1;
    private const int OperationUpdate = 2;
    private const int OperationDelete = 3;
    private const int OperationAssign = 8;
    private const int OperationRevoke = 9;

    /// <summary>Platform AuditOutcome.Succeeded — only completed mutations are audited here.</summary>
    public const int OutcomeSucceeded = 1;

    /// <summary>Every role event and the Platform operation it is filed under. Nothing else is forwarded.</summary>
    public static readonly IReadOnlyDictionary<string, int> Operations = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Created] = OperationCreate,
        [Updated] = OperationUpdate,
        [Deleted] = OperationDelete,
        [PermissionGranted] = OperationAssign,
        [PermissionRevoked] = OperationRevoke
    };
}
