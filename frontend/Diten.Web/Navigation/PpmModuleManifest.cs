namespace Diten.Web.Navigation;

/// <summary>
/// MOD-0117 tenant-shell registration manifest. It is UI discovery metadata only;
/// backend entitlement and permission enforcement remain authoritative.
/// </summary>
public static class PpmModuleManifest
{
    public const string ModuleId = "MOD-0117";
    public const string ModuleKey = "PPM";
    public const string RootRoute = "/ppm";

    /// <summary>
    /// Closed MOD-0117 Phase 2A + Phase 2B Gate L permission catalog. These are discovery metadata;
    /// the signed-JWT evaluator in Diten.PpmService remains authoritative.
    /// </summary>
    public static IReadOnlyList<string> Permissions { get; } =
    [
        "ppm.initiatives.change-lifecycle",
        "ppm.initiatives.create",
        "ppm.initiatives.read",
        "ppm.initiatives.update",
        "ppm.investment-cases.change-lifecycle",
        "ppm.investment-cases.create",
        "ppm.investment-cases.read",
        "ppm.investment-cases.update",
        "ppm.benefit-commitments.change-lifecycle",
        "ppm.benefit-commitments.create",
        "ppm.benefit-commitments.read",
        "ppm.benefit-commitments.update",
        "ppm.portfolios.change-lifecycle",
        "ppm.portfolios.create",
        "ppm.portfolios.read",
        "ppm.portfolios.update",
        "ppm.programs.change-lifecycle",
        "ppm.programs.create",
        "ppm.programs.read",
        "ppm.programs.update",
        "ppm.projects.change-lifecycle",
        "ppm.projects.create",
        "ppm.projects.read",
        "ppm.projects.update"
    ];

    public static IReadOnlyList<PpmNavigationItem> Items { get; } =
    [
        new("PPM", "PpmTitle", RootRoute, "bx bx-briefcase-alt-2", 0),
        new("PPM", "PortfoliosLabel", "/ppm/portfolios", "bx bx-layer", 10),
        new("PPM", "InitiativesLabel", "/ppm/initiatives", "bx bx-bulb", 20),
        new("PPM", "ProgramsLabel", "/ppm/programs", "bx bx-grid-alt", 30),
        new("PPM", "ProjectsLabel", "/ppm/projects", "bx bx-folder", 40),
        new("PPM", "InvestmentCasesLabel", "/ppm/investment-cases", "bx bx-line-chart", 50),
        new("PPM", "BenefitCommitmentsLabel", "/ppm/benefit-commitments", "bx bx-target-lock", 60)
    ];

    public static IPpmModuleManifestProvider Provider { get; } = new PpmModuleManifestProvider();
}

public interface IPpmModuleManifestProvider
{
    string ModuleId { get; }
    string ModuleKey { get; }
    string RootRoute { get; }
    IReadOnlyList<string> GetPermissions();
    IReadOnlyList<PpmNavigationItem> GetTenantNavigation();
}

public sealed class PpmModuleManifestProvider : IPpmModuleManifestProvider
{
    public string ModuleId => PpmModuleManifest.ModuleId;
    public string ModuleKey => PpmModuleManifest.ModuleKey;
    public string RootRoute => PpmModuleManifest.RootRoute;
    public IReadOnlyList<string> GetPermissions() => PpmModuleManifest.Permissions;
    public IReadOnlyList<PpmNavigationItem> GetTenantNavigation() => PpmModuleManifest.Items;
}

public sealed record PpmNavigationItem(
    string ModuleKey,
    string ResourceKey,
    string Route,
    string Icon,
    int Order);
