namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Q358 clean-up (decision docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md): the five
/// supplychain.shipments.* keys "shipment-tracking-pod" gave every tenant Admin while it stood in
/// <see cref="DefaultRolePermissionTemplate.AdminModules"/> (removed by C-13). The rule is <see cref="BaselineGrantRetraction"/>.
/// </summary>
public static class Q358ShipmentGrantRetraction
{
    /// <summary>The normalized module code of MOD-0183.</summary>
    public const string ModuleCode = "shipment-tracking-pod";

    public static readonly IReadOnlyList<string> ShipmentKeys =
    [
        "supplychain.shipments.read",
        "supplychain.shipments.create",
        "supplychain.shipments.dispatch",
        "supplychain.shipments.cancel",
        "supplychain.shipments.pod.capture"
    ];

    public static readonly BaselineGrantRetraction.Definition Definition = new(
        "q358", "Q358Retraction:Mode", "q358-shipment-grant-retraction",
        "docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md",
        [new(ModuleCode, ShipmentKeys)]);
}
