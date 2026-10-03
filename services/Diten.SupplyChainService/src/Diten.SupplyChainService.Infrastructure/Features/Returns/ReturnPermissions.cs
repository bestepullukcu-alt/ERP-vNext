namespace Diten.SupplyChainService.Infrastructure.Features.Returns;
public static class ReturnPermissions
{
    public const string Read = "supplychain.returns.read";
    public const string Create = "supplychain.returns.create";
    public const string Transition = "supplychain.returns.transition";
    public static string? ForTarget(string target) => target switch
    {
        "Authorized" or "Rejected" => "supplychain.returns.authorize",
        "InTransit" => "supplychain.returns.transit",
        "Cancelled" => "supplychain.returns.cancel",
        "Received" => "supplychain.returns.receive",
        "Dispositioned" => "supplychain.returns.disposition",
        "Closed" => "supplychain.returns.close",
        _ => null
    };
}
