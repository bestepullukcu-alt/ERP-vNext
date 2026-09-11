namespace Diten.AuthService.Application.Common.Authorization;

public static class PpmPermissionCatalog
{
    private static readonly string[] Resources =
        ["portfolios", "initiatives", "programs", "projects", "investment-cases", "benefit-commitments"];
    private static readonly string[] Actions = ["read", "create", "update", "change-lifecycle"];

    // Portfolio Assign-Owner Permission Publication (MOD-0117-FU01) — one explicit addition, not a
    // Resources×Actions cross product entry. Covers Portfolio Assign/Transfer only; no other resource
    // has an assign-owner action. Never automatically granted — see ExplicitGrantOnlyPermissions.
    public const string PortfoliosAssignOwner = "ppm.portfolios.assign-owner";

    public static IReadOnlyList<string> All { get; } = Resources
        .SelectMany(resource => Actions.Select(action => $"ppm.{resource}.{action}"))
        .Append(PortfoliosAssignOwner)
        .ToArray();
}
