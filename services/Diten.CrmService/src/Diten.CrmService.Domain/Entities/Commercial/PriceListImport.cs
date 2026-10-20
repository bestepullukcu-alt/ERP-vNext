namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.2: the staging area of a price list file. The file itself is never stored; only its name,
/// its SHA-256 and the parsed rows (<see cref="PriceListImportRow"/>, own collection). Each row is matched against MDM and
/// compared with the list in force (else the last published one) of the same (legal entity, country); only <c>matched</c>
/// rows become the draft list (<c>convert</c>).
/// </summary>
public sealed class PriceListImport : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public Guid LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }
    public string? LegalEntityCode { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Kind { get; set; } = PriceListKinds.Company;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;

    /// <summary><see cref="PriceListImportStatuses"/>.</summary>
    public string Status { get; set; } = PriceListImportStatuses.Uploaded;

    public int TotalRows { get; set; }
    public int Matched { get; set; }
    public int Unmatched { get; set; }
    public int Retired { get; set; }
    public int Duplicate { get; set; }
    public int InvalidPrice { get; set; }

    /// <summary>The list the rows were compared with (in force, else the last published); null = nothing to compare.</summary>
    public Guid? ComparedPriceListId { get; set; }
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Changed { get; set; }
    public int VatChanged { get; set; }

    public Guid? ConvertedPriceListId { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>One parsed row of an import (or, with <see cref="PriceListImportRowStatuses.Removed"/>, a product of the compared
/// list the file no longer carries).</summary>
public sealed class PriceListImportRow : EntityBase
{
    public Guid ImportId { get; set; }
    public int RowNo { get; set; }

    public string? SourceCode { get; set; }
    public string? SourceGtin { get; set; }

    public Guid? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? SkuCode { get; set; }
    public string? SkuName { get; set; }

    public decimal? DepotPrice { get; set; }
    public decimal? PharmacistPurchasePrice { get; set; }
    public decimal? PublicPrice { get; set; }
    public decimal? VatRate { get; set; }

    /// <summary><see cref="PriceListImportRowStatuses"/>.</summary>
    public string RowStatus { get; set; } = PriceListImportRowStatuses.Unmatched;

    /// <summary><see cref="PriceListDiffKinds"/>; null when the row is not matched.</summary>
    public string? DiffKind { get; set; }
    public decimal? PreviousDepotPrice { get; set; }
    public decimal? PreviousPharmacistPurchasePrice { get; set; }
    public decimal? PreviousPublicPrice { get; set; }
    public decimal? PreviousVatRate { get; set; }
}

public static class PriceListImportStatuses
{
    public const string Uploaded = "uploaded";
    public const string Validated = "validated";
    public const string Converted = "converted";
    public const string Discarded = "discarded";

    public static readonly IReadOnlyList<string> All = new[] { Uploaded, Validated, Converted, Discarded };
}

public static class PriceListImportRowStatuses
{
    public const string Matched = "matched";
    public const string Unmatched = "unmatched";
    public const string Retired = "retired";
    public const string Duplicate = "duplicate";
    public const string InvalidPrice = "invalid-price";
    public const string Removed = "removed";

    public static readonly IReadOnlyList<string> All = new[] { Matched, Unmatched, Retired, Duplicate, InvalidPrice, Removed };
}

public static class PriceListDiffKinds
{
    public const string Added = "added";
    public const string Changed = "changed";
    public const string Unchanged = "unchanged";
    public const string Removed = "removed";
}
