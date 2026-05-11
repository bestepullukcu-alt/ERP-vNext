using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Features.SubscriptionFeatures;

public sealed class FeatureCategory : GlobalEntity
{
    public string CategoryCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public FeatureCategoryStatus Status { get; set; } = FeatureCategoryStatus.Active;
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
