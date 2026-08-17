using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Features.SubscriptionFeatures;

public sealed class PlanFeatureMapping : GlobalEntity
{
    public Guid SubscriptionPlanId { get; set; }
    public Guid FeatureDefinitionId { get; set; }
    public PlanFeatureAvailabilityStatus AvailabilityStatus { get; set; } = PlanFeatureAvailabilityStatus.NotAvailable;
    public DateTimeOffset? EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
