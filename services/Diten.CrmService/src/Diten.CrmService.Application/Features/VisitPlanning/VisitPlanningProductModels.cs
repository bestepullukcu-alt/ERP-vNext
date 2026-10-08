namespace Diten.CrmService.Application.Features.VisitPlanning;

// WP-VP-3C (K-7) — the product-list read models of the preview (additive; kept out of VisitPlanningModels.cs).

/// <summary>A product of a doctor's next visit: id, display code, role (promo / non-promo) and source (play · rep-pick
/// · last-visit · portfolio). No play / campaign id (ARCH GATE S3-9).</summary>
public sealed record VisitProductPreview(Guid ProductId, string? ProductCode, string Role, string Source, string? ProductName = null);

/// <summary>A product the visit's role limit left out; it comes first in the doctor's next visit (K-7e, S-2).
/// <c>Reason</c>: <c>max_promo</c> / <c>max_non_promo</c>.</summary>
public sealed record OverflowProductPreview(Guid ProductId, string? ProductCode, string Role, string Reason, string? ProductName = null);

/// <summary>How many doctors' plans in the period tell the product (any of their visits).</summary>
public sealed record ProductDistributionDto(Guid ProductId, string? ProductCode, int DoctorCount, string? ProductName = null);

/// <summary>A week's visits telling the product, and of those the ones where it is promo.</summary>
public sealed record ProductVisitCountDto(Guid ProductId, string? ProductCode, int Visits, int PromoVisits, string? ProductName = null);

/// <summary>WP-VP-3C (K-7c) — whether the rep's product portfolio is defined. No portfolio data exists yet, so it is
/// always <c>undefined</c> and the portfolio source never fills a list automatically.</summary>
public static class PortfolioStatuses
{
    public const string Defined = "defined";
    public const string Undefined = "undefined";
}
