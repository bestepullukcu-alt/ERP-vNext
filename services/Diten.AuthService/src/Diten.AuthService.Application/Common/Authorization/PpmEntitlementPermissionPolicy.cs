namespace Diten.AuthService.Application.Common.Authorization;

public interface IPpmEntitlementPermissionPolicy
{
    bool Applies(string? moduleCode);
}

public sealed class PpmEntitlementPermissionPolicy : IPpmEntitlementPermissionPolicy
{
    public const string ModuleCode = "PPM";

    public bool Applies(string? moduleCode) =>
        string.Equals(moduleCode, ModuleCode, StringComparison.Ordinal);
}
