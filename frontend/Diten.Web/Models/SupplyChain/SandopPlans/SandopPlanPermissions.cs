namespace Diten.Web.Models.SupplyChain.SandopPlans;

/// <summary>MOD-0190 permission keys, mirrored from SandopPermissions.cs (backend). Display decisions only — the
/// service's [SandopPermission] stays the authority.</summary>
public static class SandopUiPermissions
{
    public const string Read = "supplychain.sandop-plans.read";
    public const string Create = "supplychain.sandop-plans.create";
    public const string Capture = "supplychain.sandop-plans.snapshot.capture";
    public const string SignOff = "supplychain.sandop-plans.sign-off.record";
}

/// <summary>The page's display flags, computed once on the server and handed to the script as JSON.</summary>
public sealed record SandopPagePermissions(bool CanCreate, bool CanCapture, bool CanSignOff)
{
    public static SandopPagePermissions From(Func<string, bool> has) =>
        new(has(SandopUiPermissions.Create), has(SandopUiPermissions.Capture), has(SandopUiPermissions.SignOff));
}
