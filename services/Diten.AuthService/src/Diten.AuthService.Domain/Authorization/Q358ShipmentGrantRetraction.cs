using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Q358 clean-up (decision docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md). While
/// "shipment-tracking-pod" stood in <see cref="DefaultRolePermissionTemplate.AdminModules"/>, every tenant activated got the
/// five supplychain.shipments.* keys on its Admin role as SYSTEM grants. Removing the list entry (C-13) does not take them
/// back: default-role setup only adds, and the entitlement sync skips a (role, permission) pair that already has a grant of
/// any source — so even an entitled tenant keeps them as System. This is the pure decision; the Mongo executor is
/// <c>Q358ShipmentGrantRetractionRunner</c>.
/// <para>Per tenant Admin role, per System-sourced Shipment grant:</para>
/// <list type="bullet">
/// <item>tenant entitled to the module → <see cref="Action.ConvertToModule"/> (access kept, source corrected);</item>
/// <item>tenant NOT entitled → <see cref="Action.SoftDelete"/> (the widening is taken back);</item>
/// <item>entitlement unknown (Platform unreachable / unconfigured) → <see cref="Action.Skip"/>: nothing is changed.</item>
/// </list>
/// Module and Manual grants, other roles (Viewer's baseline read, SuperAdmin) and other keys are never touched.
/// </summary>
public static class Q358ShipmentGrantRetraction
{
    /// <summary>The normalized module code of MOD-0183 (what <see cref="ModulePermissionResolver.NormalizeModuleCode"/> yields).</summary>
    public const string ModuleCode = "shipment-tracking-pod";

    /// <summary>The role the widening reached (<see cref="DefaultRolePermissionTemplate.AdminModules"/> is the Admin baseline).</summary>
    public const string AdminRoleName = "Admin";

    public static readonly IReadOnlyList<string> ShipmentKeys =
    [
        "supplychain.shipments.read",
        "supplychain.shipments.create",
        "supplychain.shipments.dispatch",
        "supplychain.shipments.cancel",
        "supplychain.shipments.pod.capture"
    ];

    public const string ConvertedEventName = "role_permission_source_corrected";
    public const string RevokedEventName = "role_permission_revoked";
    public const string AuditSource = "q358-shipment-grant-retraction";
    public const string ConfigurationKey = "Q358Retraction:Mode";
    public const string DecisionRef = "docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md";

    public enum Mode { Off, DryRun, Apply }

    public enum Action { ConvertToModule, SoftDelete, Skip }

    /// <summary>A live grant as the decision needs it.</summary>
    public sealed record Grant(Guid GrantId, Guid TenantId, Guid RoleId, Guid PermissionId, string PermissionKey, GrantSource Source);

    public sealed record Planned(Grant Grant, Action Action);

    /// <summary>Parses the configured mode; anything unrecognised is <see cref="Mode.Off"/> (fail safe: never Apply by accident).</summary>
    public static Mode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dryrun" or "dry-run" => Mode.DryRun,
        "apply" => Mode.Apply,
        _ => Mode.Off
    };

    /// <summary>
    /// The decision for one tenant. <paramref name="entitledModuleCodes"/> null means UNKNOWN (the read failed) — every
    /// candidate is skipped. Only System-sourced grants of the five keys are candidates.
    /// </summary>
    public static IReadOnlyList<Planned> Plan(IEnumerable<Grant> adminGrants, IReadOnlyCollection<string>? entitledModuleCodes)
    {
        var candidates = adminGrants
            .Where(g => g.Source == GrantSource.System && ShipmentKeys.Contains(g.PermissionKey, StringComparer.Ordinal))
            .OrderBy(g => g.PermissionKey, StringComparer.Ordinal)
            .ToList();
        if (entitledModuleCodes is null)
            return candidates.Select(g => new Planned(g, Action.Skip)).ToList();
        var entitled = entitledModuleCodes.Any(c => ModulePermissionResolver.NormalizeModuleCode(c) == ModuleCode);
        return candidates.Select(g => new Planned(g, entitled ? Action.ConvertToModule : Action.SoftDelete)).ToList();
    }

    /// <summary>The id of THE audit row of one grant's retraction: written once however often the run is repeated.</summary>
    public static Guid AuditId(Guid tenantId, Guid roleId, string permissionKey, Action action)
        => Deterministic($"q358-retraction|{action}|{tenantId:N}|{roleId:N}|{permissionKey}");

    /// <summary>The id of the row that withdraws <paramref name="auditId"/> when its change did not happen.</summary>
    public static Guid NotAppliedAuditId(Guid auditId) => Deterministic($"q358-retraction-not-applied|{auditId:N}");

    private static Guid Deterministic(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}
