using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Entitlements;

public sealed record EntitlementReconciliationRow(string Action, Guid GrantId, Guid RoleId,
    string RoleName, Guid PermissionId, string PermissionKey, string PermissionModule,
    PermissionScope PermissionScope, GrantSource GrantSource, string SourceModuleCode);
public sealed record EntitlementOperatorSnapshot(Guid UserId, Guid TenantId, string NormalizedEmail,
    bool IsAuthorized, string Fingerprint);
public sealed record EntitlementAuthoritySnapshot(Guid RequestedTenantId, string ModuleCode,
    string RequestedUri, string OperatorStatusRequestedUri, string TenantStatusRequestedUri,
    string NormalizedOperatorEmail, bool OperatorActive, bool TenantActive,
    IReadOnlyList<string> PermissionKeys, string Fingerprint)
{
    public IReadOnlyList<string> PermissionKeys { get; } = Array.AsReadOnly(PermissionKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
}
// Fingerprint covers COMPLETE relevant rows, deleted rows, holders and token metadata, never secrets.
public sealed record EntitlementLocalSnapshot(Guid TenantId, long RoleAssignmentVersion,
    IReadOnlyList<Permission> Permissions, IReadOnlyList<Role> Roles,
    IReadOnlyList<RolePermission> Grants, IReadOnlyList<Guid> AffectedHolderIds,
    IReadOnlyList<Guid> ActiveRefreshTokenIds, EntitlementOperatorSnapshot Operator,
    string Fingerprint, string QuiescenceFingerprint)
{
    public IReadOnlyList<Permission> Permissions { get; } = Array.AsReadOnly(Permissions.ToArray());
    public IReadOnlyList<Role> Roles { get; } = Array.AsReadOnly(Roles.ToArray());
    public IReadOnlyList<RolePermission> Grants { get; } = Array.AsReadOnly(Grants.ToArray());
    public IReadOnlyList<Guid> AffectedHolderIds { get; } = Array.AsReadOnly(AffectedHolderIds.Order().ToArray());
    public IReadOnlyList<Guid> ActiveRefreshTokenIds { get; } = Array.AsReadOnly(ActiveRefreshTokenIds.Order().ToArray());
}
public sealed record EntitlementReconciliationPlan(int SchemaVersion, Guid OperationId, Guid TenantId,
    string ModuleCode, Guid ActorId, string ActorType, string OperatorFingerprint,
    string AuthorityFingerprint, string LocalFingerprint, string QuiescenceFingerprint,
    long ExpectedRoleAssignmentVersion, IReadOnlyList<Guid> AffectedHolderIds,
    IReadOnlyList<Guid> ActiveRefreshTokenIds, IReadOnlyList<EntitlementReconciliationRow> Rows,
    string ProvenanceSha256, string BinarySha256, string SourceHead, DateTimeOffset CreatedAtUtc)
{
    public IReadOnlyList<Guid> AffectedHolderIds { get; } = Array.AsReadOnly(AffectedHolderIds.Order().ToArray());
    public IReadOnlyList<Guid> ActiveRefreshTokenIds { get; } = Array.AsReadOnly(ActiveRefreshTokenIds.Order().ToArray());
    public IReadOnlyList<EntitlementReconciliationRow> Rows { get; } = Array.AsReadOnly(Rows
        .OrderBy(x => x.Action, StringComparer.Ordinal).ThenBy(x => x.RoleName, StringComparer.Ordinal)
        .ThenBy(x => x.PermissionKey, StringComparer.Ordinal).ThenBy(x => x.GrantId).ToArray());
    public const string Module = "product-item-sku-master";
    public static readonly Guid TargetTenant = Guid.Parse("74355e70-4c7d-410c-8cf6-db5fe3b9547f");
    public static readonly Guid AdminTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid RemovedGrant = Guid.Parse("dc241b94-3a12-4825-bcb0-b66a7189124f");
    public const string RequiredPermission = "auth.roles.assign-permission";
    public string CanonicalJson()
    {
        if (SchemaVersion != 1 || OperationId == Guid.Empty || TenantId != TargetTenant || ModuleCode != Module
            || ActorId == Guid.Empty || ActorType != "PlatformAdministrator"
            || Rows.Count != 7 || Rows.Count(r => r.Action == "add") != 6 || Rows.Count(r => r.Action == "remove") != 1
            || Rows.Any(r => r.GrantSource != GrantSource.Module || r.SourceModuleCode != Module)
            || Rows.Single(r => r.Action == "remove").GrantId != RemovedGrant
            || Rows.Select(r => r.GrantId).Distinct().Count() != 7
            || AffectedHolderIds.Distinct().Count() != AffectedHolderIds.Count
            || ActiveRefreshTokenIds.Distinct().Count() != ActiveRefreshTokenIds.Count)
            throw new InvalidOperationException("PLAN_INVARIANT_INVALID");
        return JsonSerializer.Serialize(this);
    }
    public string Sha256() => Hash(CanonicalJson());
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static Guid DeterministicId(Guid operationId, string discriminator)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:D}\n{discriminator}"))[..16]);
}
public sealed record EntitlementOperationReceipt(Guid OperationId, string PlanSha256, string State,
    string? Reason, string PostStateFingerprint, long RoleAssignmentVersion, bool ManualHold);
public sealed record EntitlementLocalCommitResult(string State, EntitlementOperationReceipt? Receipt);
