namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// Phase-1.5 (class J) projection: one ACTIVE AccountContactLink plus the linked account type, read in bulk. It exists
/// so a contact-level criterion can ask about its account relationship without a per-candidate query, and so the
/// territory sources can map a contact to the accounts that carry its coverage.
/// <para><see cref="AccountName"/> (WP-SEG-F) is an ADDITIVE display field carried on the SAME bulk account read that
/// already resolves <see cref="AccountType"/> — a single extra projection field, no extra query and no per-candidate
/// read. It feeds the contact sample's "workplace" secondary label only; no rule is ever evaluated against it.</para>
/// </summary>
public sealed record SegmentLinkProjection(
    Guid ContactId,
    Guid AccountId,
    string RoleCode,
    bool IsPrimary,
    string? AccountType,
    string? AccountName = null);
