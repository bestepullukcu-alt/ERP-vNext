using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// The pure decision behind taking back a TEMPORARY widening of the tenant Admin baseline. While a module code stood in
/// <see cref="DefaultRolePermissionTemplate.AdminModules"/>, every tenant activated got that module's keys on its Admin role
/// as SYSTEM grants. Removing the list entry does not take them back: default-role setup only adds, and the entitlement sync
/// skips a (role, permission) pair that already has a grant of any source — so even an entitled tenant keeps them as System.
/// Each widening is one <see cref="Definition"/> (Q358: <see cref="Q358ShipmentGrantRetraction"/>, G7:
/// <see cref="G7SandopCapacityGrantRetraction"/>); the Mongo executor is <c>Q358ShipmentGrantRetractionRunner</c>.
/// <para>Per tenant Admin role, per System-sourced grant of a definition's key:</para>
/// <list type="bullet">
/// <item>tenant entitled to THAT key's module → <see cref="Action.ConvertToModule"/> (access kept, source corrected);</item>
/// <item>tenant NOT entitled to it → <see cref="Action.SoftDelete"/> (the widening is taken back);</item>
/// <item>entitlement unknown (Platform unreachable / unconfigured) → <see cref="Action.Skip"/>: nothing is changed.</item>
/// </list>
/// Module and Manual grants, other roles (Viewer's baseline read, SuperAdmin) and other keys are never touched.
/// </summary>
public static class BaselineGrantRetraction
{
    public const string ConvertedEventName = "role_permission_source_corrected";
    public const string RevokedEventName = "role_permission_revoked";
    public const string AdminRoleName = "Admin";

    public enum Mode { Off, DryRun, Apply }

    public enum Action { ConvertToModule, SoftDelete, Skip }

    /// <summary>One module of a widening and the keys it brought. <paramref name="ModuleCode"/> is normalized
    /// (what <see cref="ModulePermissionResolver.NormalizeModuleCode"/> yields).</summary>
    public sealed record RetractedModule(string ModuleCode, IReadOnlyList<string> Keys);

    /// <param name="Name">Short id; prefixes the deterministic audit ids (<c>{Name}-retraction|…</c>).</param>
    public sealed record Definition(
        string Name, string ConfigurationKey, string AuditSource, string DecisionRef, IReadOnlyList<RetractedModule> Modules)
    {
        public IReadOnlyList<string> AllKeys => Modules.SelectMany(m => m.Keys).ToList();

        public string? ModuleOf(string key) => Modules.FirstOrDefault(m => m.Keys.Contains(key, StringComparer.Ordinal))?.ModuleCode;
    }

    /// <summary>A live grant as the decision needs it.</summary>
    public sealed record Grant(Guid GrantId, Guid TenantId, Guid RoleId, Guid PermissionId, string PermissionKey, GrantSource Source);

    /// <param name="ModuleCode">The module the key belongs to (the Module grant's source after a conversion).</param>
    public sealed record Planned(Grant Grant, Action Action, string ModuleCode);

    /// <summary>Parses the configured mode; anything unrecognised is <see cref="Mode.Off"/> (fail safe: never Apply by accident).</summary>
    public static Mode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dryrun" or "dry-run" => Mode.DryRun,
        "apply" => Mode.Apply,
        _ => Mode.Off
    };

    /// <summary>
    /// The decision for one tenant. <paramref name="entitledModuleCodes"/> null means UNKNOWN (the read failed) — every
    /// candidate is skipped. Only System-sourced grants of the definition's keys are candidates; each is decided by the
    /// entitlement of its OWN module.
    /// </summary>
    public static IReadOnlyList<Planned> Plan(Definition definition, IEnumerable<Grant> adminGrants, IReadOnlyCollection<string>? entitledModuleCodes)
    {
        var candidates = adminGrants
            .Where(g => g.Source == GrantSource.System && definition.ModuleOf(g.PermissionKey) is not null)
            .OrderBy(g => g.PermissionKey, StringComparer.Ordinal)
            .ToList();
        if (entitledModuleCodes is null)
            return candidates.Select(g => new Planned(g, Action.Skip, definition.ModuleOf(g.PermissionKey)!)).ToList();
        var entitled = entitledModuleCodes.Select(ModulePermissionResolver.NormalizeModuleCode).ToHashSet(StringComparer.Ordinal);
        return candidates.Select(g =>
        {
            var module = definition.ModuleOf(g.PermissionKey)!;
            return new Planned(g, entitled.Contains(module) ? Action.ConvertToModule : Action.SoftDelete, module);
        }).ToList();
    }

    /// <summary>The id of THE audit row of one grant's retraction: written once however often the run is repeated.</summary>
    public static Guid AuditId(Definition definition, Guid tenantId, Guid roleId, string permissionKey, Action action)
        => Deterministic($"{definition.Name}-retraction|{action}|{tenantId:N}|{roleId:N}|{permissionKey}");

    /// <summary>The id of the row that withdraws <paramref name="auditId"/> when its change did not happen.</summary>
    public static Guid NotAppliedAuditId(Definition definition, Guid auditId)
        => Deterministic($"{definition.Name}-retraction-not-applied|{auditId:N}");

    private static Guid Deterministic(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}

/// <summary>G7 clean-up (decision docs/records/decisions/2026-10/mvp6-0190-0192-g7-role-grants-owner-decision-01.md): the
/// eight S&amp;OP (MOD-0190) and Capacity (MOD-0192) keys the "sop-workflow-signoffs" / "capacity-planning" Admin baseline
/// entries gave tenants activated on hosts where the modules are composed (never production: they are composed only on a
/// non-production demand fixture). The Viewer's two .read keys come from the read rule and are not part of G7.</summary>
public static class G7SandopCapacityGrantRetraction
{
    public const string SopModuleCode = "sop-workflow-signoffs";
    public const string CapacityModuleCode = "capacity-planning";

    public static readonly BaselineGrantRetraction.Definition Definition = new(
        "g7", "G7Retraction:Mode", "g7-sandop-capacity-grant-retraction",
        "docs/records/decisions/2026-10/mvp6-0190-0192-g7-role-grants-owner-decision-01.md",
        [
            new(SopModuleCode,
            [
                "supplychain.sandop-plans.read",
                "supplychain.sandop-plans.create",
                "supplychain.sandop-plans.snapshot.capture",
                "supplychain.sandop-plans.sign-off.record"
            ]),
            new(CapacityModuleCode,
            [
                "supplychain.capacity-plans.read",
                "supplychain.capacity-plans.create",
                "supplychain.capacity-plans.scenario.create",
                "supplychain.capacity-plans.evaluate"
            ])
        ]);
}
