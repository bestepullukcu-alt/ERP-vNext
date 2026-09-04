namespace Diten.AuthService.Application.Common.Authorization;

public static class PpmPermissionCatalog
{
    private static readonly string[] Resources =
        ["portfolios", "initiatives", "programs", "projects", "investment-cases", "benefit-commitments"];
    private static readonly string[] Actions = ["read", "create", "update", "change-lifecycle"];

    public static IReadOnlyList<string> All { get; } = Resources
        .SelectMany(resource => Actions.Select(action => $"ppm.{resource}.{action}"))
        .ToArray();
}
