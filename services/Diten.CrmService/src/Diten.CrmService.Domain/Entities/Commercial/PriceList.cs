namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.1: a price list of one selling legal entity × country × currency. Its items live in their
/// own collection (<see cref="PriceListItem"/>). Approved through ONE MOD-0023 round at a time (four eyes, D13); once
/// <c>published</c> neither the list nor its items change — a change is a new version (a new draft).
/// <para>The phase of a published list (planned / in force / past) is DERIVED at read time from the day and the other
/// published lists of the same (legal entity, country): no scheduler, no write when a list comes into force (§4.1.2).</para>
/// </summary>
public sealed class PriceList : EntityBase, IWorkflowApprovable
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The selling legal entity (MDM) and its snapshot at creation.</summary>
    public Guid LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }
    public string? LegalEntityCode { get; set; }

    /// <summary>The legal entity's country (MDM <c>LegalEntity.CountryCode</c>); never chosen by hand.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>ISO 4217.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary><see cref="PriceListKinds"/>.</summary>
    public string Kind { get; set; } = PriceListKinds.Company;

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    /// <summary><see cref="PriceListStatuses"/>.</summary>
    public string Status { get; set; } = PriceListStatuses.Draft;

    /// <summary>The list family (the first version's id): versions 1, 2, … share it.</summary>
    public Guid FamilyId { get; set; }
    public int ListVersion { get; set; } = 1;
    public Guid? PreviousPriceListId { get; set; }
    public Guid? SourceImportId { get; set; }
    public int ItemCount { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }

    public List<ClaimReviewRound> ReviewRounds { get; set; } = new();
    public List<RegulatoryTextDecision> Decisions { get; set; } = new();

    public bool IsDraft() => Status == PriceListStatuses.Draft;
    public bool IsPublished() => Status == PriceListStatuses.Published;
}

public static class PriceListKinds
{
    public const string Official = "official";
    public const string Company = "company";

    public static readonly IReadOnlyList<string> All = new[] { Official, Company };
}

public static class PriceListStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Published = "published";
    public const string Discarded = "discarded";

    public static readonly IReadOnlyList<string> All = new[] { Draft, InReview, Published, Discarded };
}

/// <summary>The derived phase of a published list (§4.1.1); the badges of screen D.</summary>
public static class PriceListPhases
{
    public const string Planned = "planned";
    public const string InForce = "in-force";
    public const string Past = "past";

    public static readonly IReadOnlyList<string> All = new[] { Planned, InForce, Past };
}

/// <summary>The price tiers (§4.1, D11): depot, pharmacist purchase (the default), public.</summary>
public static class PriceTiers
{
    public const string Depot = "depot";
    public const string PharmacistPurchase = "pharmacist-purchase";
    public const string Public = "public";

    public static readonly IReadOnlyList<string> All = new[] { Depot, PharmacistPurchase, Public };
}
