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
/// Central, closed entitlement-to-permission strategy selection. PPM is explicit-grant only;
/// every other module retains the established bridge behavior.
/// </summary>
public sealed class EntitlementPermissionPolicy : IEntitlementPermissionPolicy
{
    public const string PpmModuleCode = "PPM";

    public EntitlementPermissionMode Resolve(string? moduleCode)
        => string.Equals(moduleCode?.Trim(), PpmModuleCode, StringComparison.OrdinalIgnoreCase)
            ? EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval
            : EntitlementPermissionMode.LegacyAutoGrantAndRevoke;
}
