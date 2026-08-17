using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

public sealed class BillingInvoiceCounter : TenantScopedEntity
{
    public required int Year { get; init; }
    public required string CounterName { get; init; }
    public required long NextValue { get; set; }
}
