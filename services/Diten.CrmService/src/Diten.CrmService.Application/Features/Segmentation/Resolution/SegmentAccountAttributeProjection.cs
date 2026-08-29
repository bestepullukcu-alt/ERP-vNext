namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>Bulk projection of AccountAttributeValue rows for the <c>account.attribute</c> criterion (the closest real
/// field behind a tenant-authored key such as "tier"). Read once for the whole candidate set.</summary>
public sealed record SegmentAccountAttributeProjection(
    Guid AccountId,
    string AttributeCode,
    string? Value);
