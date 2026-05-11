using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Features.SubscriptionFeatures;

public sealed class FeatureDefinition : GlobalEntity
{
    public string FeatureCode { get; set; } = string.Empty;
    public string FeatureSlug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public FeatureDefinitionStatus Status { get; set; } = FeatureDefinitionStatus.Draft;
    public bool IsCoreFeature { get; set; }
    public int SortOrder { get; set; }
    public string? OptionalFeatureFlagKey { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
