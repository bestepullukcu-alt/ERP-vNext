using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>
/// WP-CL-BE-6 — "where is this claim used?" for one logical claim (<paramref name="ClaimCode"/>, every version): the
/// knowledge contents that reference it, the knowledge paths that placed any of its records (WP-KP-4; the retired content
/// set is no longer a source), and the engagement journeys whose stage's recommended knowledge path steps through such a
/// content. Archived rows are excluded. There is NO
/// country restriction on the read (D1) — <paramref name="CountryCode"/> is only a filter on the groups.
/// </summary>
public sealed record GetClaimUsageQuery(string ClaimCode, string? CountryCode = null)
    : IRequest<Response<ClaimUsageDto>>;

/// <summary>Usage grouped by country (<see cref="ClaimUsageGroups.Global"/> for global use).</summary>
public sealed record ClaimUsageDto(string ClaimCode, IReadOnlyList<ClaimUsageGroupDto> Groups);

public sealed record ClaimUsageGroupDto(string CountryCode, IReadOnlyList<ClaimUsageItemDto> Items);

/// <summary>One using object. <see cref="Type"/> is <c>content</c> / <c>knowledge-path</c> / <c>journey</c>;
/// <see cref="Via"/> is set on a journey (the content code it reaches the claim through). <see cref="ClaimNeedsReview"/>
/// is true when the bound claim record / country version is <c>review-required</c>. <see cref="CountryCode"/> is the
/// knowledge path's country (WP-KP-4), null for the other types.</summary>
public sealed record ClaimUsageItemDto(
    string Type,
    Guid Id,
    string Code,
    string Name,
    string? LanguageCode,
    string? Version,
    string Status,
    string? Via,
    bool ClaimNeedsReview,
    string? CountryCode = null);

public static class ClaimUsageGroups
{
    public const string Global = "GLOBAL";
}

public static class ClaimUsageItemTypes
{
    public const string Content = "content";

    /// <summary>WP-KP-4 — a knowledge path whose claims (KP-1 <c>KnowledgePath.Claims</c>) hold a record of the code. It
    /// replaces the retired <c>content-set</c> source, which is no longer produced.</summary>
    public const string KnowledgePath = "knowledge-path";

    public const string Journey = "journey";
}
