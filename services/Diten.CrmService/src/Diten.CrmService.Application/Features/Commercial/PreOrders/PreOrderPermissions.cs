namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>WP-VW-W6 W6-BE-a — MOD-0168 §14 (PKS-001, namespace <c>commercial.*</c>, K-O2). Definitions only: the role
/// seed / grant is F-RBAC (W6-CFG).</summary>
public static class PreOrderPermissions
{
    public const string Read = "commercial.pre-orders.read";
    public const string ReadAll = "commercial.pre-orders.read-all";
    public const string Create = "commercial.pre-orders.create";
    public const string Submit = "commercial.pre-orders.submit";
    public const string Approve = "commercial.pre-orders.approve";
    public const string Cancel = "commercial.pre-orders.cancel";
    public const string RecordDepotResponse = "commercial.pre-orders.record-depot-response";
    public const string RetryTransmission = "commercial.pre-orders.retry-transmission";
    public const string Export = "commercial.pre-orders.export";
    public const string SettingsRead = "commercial.pre-order-settings.read";
    public const string SettingsUpdate = "commercial.pre-order-settings.update";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Read, ReadAll, Create, Submit, Approve, Cancel, RecordDepotResponse, RetryTransmission, Export, SettingsRead, SettingsUpdate
    };
}
