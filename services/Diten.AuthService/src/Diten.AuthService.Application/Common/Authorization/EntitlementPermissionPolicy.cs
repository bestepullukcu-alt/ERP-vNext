namespace Diten.AuthService.Application.Common.Authorization;

public enum EntitlementPermissionMode
{
    LegacyAutoGrantAndRevoke = 0,
    ExplicitOnlyPreserveOnEntitlementRemoval = 1
}

public interface IEntitlementPermissionPolicy
{
    EntitlementPermissionMode Resolve(string? moduleCode);
}

/// <summary>
/// Central, closed entitlement-to-permission strategy selection. PPM and MOD-0355 are
/// explicit-grant only; every other module retains the established bridge behavior.
/// </summary>
public sealed class EntitlementPermissionPolicy : IEntitlementPermissionPolicy
{
    public const string PpmModuleCode = "PPM";
    public const string Mod0355ModuleCode = "MOD-0355";

    public EntitlementPermissionMode Resolve(string? moduleCode)
        => string.Equals(moduleCode?.Trim(), PpmModuleCode, StringComparison.OrdinalIgnoreCase)
            // The legacy synchronization service passes its canonical lower-case module code.
            // Whitespace aliases remain rejected; ordinal operation/permission identifiers are
            // independently enforced by PermissionCatalogManifestV1.
            || string.Equals(moduleCode, Mod0355ModuleCode, StringComparison.OrdinalIgnoreCase)
            ? EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval
            : EntitlementPermissionMode.LegacyAutoGrantAndRevoke;
}
