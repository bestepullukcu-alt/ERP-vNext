namespace Diten.Web.Models.ManagementGovernance.Dws;

public static class DwsFrontendPermissions
{
    public const string Create = "management-governance.dws.create";
    public const string Update = "management-governance.dws.update";
    public const string Read = "management-governance.dws.read";
    public const string Validate = "management-governance.dws.validate";
    public const string Compare = "management-governance.dws.compare";
    public const string Baseline = "management-governance.dws.baseline";

    public static readonly IReadOnlySet<string> Exact = new HashSet<string>(StringComparer.Ordinal)
    {
        Create, Update, Read, Validate, Compare, Baseline
    };
}

public sealed record DwsFrontendIndexViewModel(
    bool GatewayEnabled,
    bool CanRead,
    bool CanValidate,
    bool CanCompare);
