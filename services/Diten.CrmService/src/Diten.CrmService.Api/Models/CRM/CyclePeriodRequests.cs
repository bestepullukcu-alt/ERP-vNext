namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0165 FU06/FU07 request bodies. <c>TenantId</c> appears in none of them — it is resolved server-side from the
/// claim — and neither does <c>CycleStatus</c>: the lifecycle moves only through the activate / close endpoints, so a
/// status can never be set as a side effect of an edit.
/// <para>FU07 adds the discriminated scope. <c>ScopeType</c> names the level; exactly one of <c>CountryScope</c>,
/// <c>LegalEntityId</c> and <c>BusinessUnitId</c> belongs to it, and sending a second one is refused rather than
/// ignored. <c>BusinessUnitSource</c> is absent on purpose — provenance is stamped by the server, not claimed by the
/// caller.</para>
/// </summary>
public sealed class CreateCyclePeriodRequest
{
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }

    /// <summary>Inclusive last day of the period.</summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>tenant | country | legal-entity | business-unit. Required: a period must know where it lives.</summary>
    public string? ScopeType { get; set; }

    /// <summary>ISO alpha-2, when ScopeType is <c>country</c>. Validated against the governed reference set.</summary>
    public string? CountryScope { get; set; }

    /// <summary>MDM legal entity id, when ScopeType is <c>legal-entity</c>. Proved referenceable before persistence.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>Published business-unit code, when ScopeType is <c>business-unit</c>.</summary>
    public string? BusinessUnitId { get; set; }

    /// <summary>Informational only, and only at <c>business-unit</c> scope: the country the author was filtering by
    /// when they chose the unit. It is stored so a reader sees "TR / alpha" instead of a bare code, and it is NOT part
    /// of the period's identity — uniqueness, the overlap ban and the resolver all ignore it.</summary>
    public string? BusinessUnitCountryContext { get; set; }

    public string? Description { get; set; }
}

/// <summary>An edit. <c>CycleCode</c> is absent on purpose: the stable business key is never renamed.
/// <para>An omitted <c>ScopeType</c> means "leave the period where it is", so a caller written against FU06 cannot move
/// one by accident. Supplying a DIFFERENT one is a 409: the scope is part of the identity.</para></summary>
public sealed class UpdateCyclePeriodRequest
{
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? ScopeType { get; set; }
    public string? CountryScope { get; set; }
    public Guid? LegalEntityId { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? BusinessUnitCountryContext { get; set; }
    public string? Description { get; set; }
    public int? ExpectedVersion { get; set; }
}
