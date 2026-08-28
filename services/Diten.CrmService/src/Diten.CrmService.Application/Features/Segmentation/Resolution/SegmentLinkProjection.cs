namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// Phase-1.5 (class J) projection: one ACTIVE AccountContactLink plus the linked account type, read in bulk. It exists
/// so a contact-level criterion can ask about its account relationship without a per-candidate query, and so the
/// territory sources can map a contact to the accounts that carry its coverage.
/// </summary>
public sealed record SegmentLinkProjection(
    Guid ContactId,
    Guid AccountId,
    string RoleCode,
    bool IsPrimary,
    string? AccountType);
