using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0165 FU06/FU07 — the create/edit form. FU07 added the discriminated scope (level + exactly one reference), which
/// took the user-field count from 8 to 11 and moved this module from the Golden <b>Slim</b> reference to the Golden
/// <b>Compact</b> one: a separate Create / Edit / Details page instead of an offcanvas.
/// <para><c>CycleStatus</c> is deliberately NOT a form field: the lifecycle moves only through the Activate and Close
/// actions, and showing a status in the form would suggest an author can put a period live by editing it — which would
/// also bypass the moment the overlap ban is checked. <c>BusinessUnitSource</c> is not one either: provenance is
/// stamped by the server, never claimed by the caller.</para>
/// <para>Optional numeric/date fields are nullable so the generated client validation does not demand a value the
/// runtime treats as optional.</para>
/// </summary>
public sealed class CyclePeriodEditViewModel
{
    public Guid? CyclePeriodId { get; set; }

    /// <summary>Stable business key. Editable on create only — a period's code is never renamed.</summary>
    [Required]
    public string CycleCode { get; set; } = string.Empty;

    [Required]
    public string CycleName { get; set; } = string.Empty;

    /// <summary>The planning year, authored rather than derived from <see cref="StartDate"/>: a period may cross a year
    /// boundary and which year it counts as is a business decision.</summary>
    [Required]
    public int? Year { get; set; }

    [Required]
    public int? SequenceInYear { get; set; }

    [Required]
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>Inclusive last day.</summary>
    [Required]
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// FU07 — which LEVEL this period lives at: tenant / country / legal-entity / business-unit. Required, and
    /// <b>read-only once the period exists</b>: the scope is half of the period's identity, so a period at the wrong
    /// address is closed and a new one opened rather than moved.
    /// </summary>
    [Required]
    public string ScopeType { get; set; } = "tenant";

    /// <summary>ISO alpha-2, when <see cref="ScopeType"/> is <c>country</c>.</summary>
    public string? CountryScope { get; set; }

    /// <summary>MDM legal entity, when <see cref="ScopeType"/> is <c>legal-entity</c>.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>Published business-unit code, when <see cref="ScopeType"/> is <c>business-unit</c>.</summary>
    public string? BusinessUnitId { get; set; }

    /// <summary>
    /// The country the author filtered by when picking the business unit — posted from the business-unit block's own
    /// country selector and kept as INFORMATIONAL CONTEXT, so a reader sees "TR / alpha" instead of a bare code.
    /// <para>It is not a country-SCOPED period and never part of the identity: the runtime stores it only at
    /// <c>business-unit</c> scope and ignores it for uniqueness, the overlap ban and resolution.</para>
    /// </summary>
    public string? BusinessUnitCountryContext { get; set; }

    public string? Description { get; set; }

    public int? ExpectedVersion { get; set; }

    // ── server-rendered context (never posted back as authority) ───────────────────────────────────────────────────

    /// <summary>Server-stamped provenance of <see cref="BusinessUnitId"/>: <c>territory</c> or <c>manual</c>. Shown so
    /// an author can see whether the unit is covered by a live field plan; it is display-only.</summary>
    public string? BusinessUnitSource { get; set; }

    /// <summary>Lifecycle status of an existing period, for the Details page and the Edit page's guards.</summary>
    public string? CycleStatus { get; set; }

    public bool IsDraft => string.Equals(CycleStatus, "draft", StringComparison.OrdinalIgnoreCase);

    public bool IsActive => string.Equals(CycleStatus, "active", StringComparison.OrdinalIgnoreCase);

    public bool IsClosed => string.Equals(CycleStatus, "closed", StringComparison.OrdinalIgnoreCase);

    /// <summary>True while the period is still editable — an active period accepts only its name and description, and
    /// a closed one accepts nothing.</summary>
    public bool CanEditWindow => !IsActive && !IsClosed;

    public CyclePeriodScopeOptionsViewModel ScopeOptions { get; set; } = new();
}

/// <summary>
/// FU07 — the cascading selector's option sources, each with its own readiness flag.
/// <para>The flags are the point: an empty list because a governed set is unpublished, an empty list because MDM could
/// not be reached, and an empty list because no territory plan matches are three different situations that need three
/// different messages. A silent empty dropdown tells the author nothing and leaves them no way to act. Substituting a
/// hardcoded list is forbidden in all three cases — an option the platform does not know would be authored and then
/// refused at save.</para>
/// </summary>
public sealed class CyclePeriodScopeOptionsViewModel
{
    public List<string> ScopeTypes { get; set; } = [];

    public List<CyclePeriodScopeOptionViewModel> Countries { get; set; } = [];
    public bool CountryReady { get; set; }

    public List<CyclePeriodScopeOptionViewModel> LegalEntities { get; set; } = [];
    public bool LegalEntityReady { get; set; }

    public List<CyclePeriodScopeOptionViewModel> BusinessUnits { get; set; } = [];
    public bool BusinessUnitReady { get; set; }

    /// <summary>True when the business-unit list was derived from matching territory plans, false when it fell back to
    /// the governed <c>business-unit</c> vocabulary. Both are governed; only one is a plan.</summary>
    public bool BusinessUnitFromTerritory { get; set; }

    public string CountrySetCode { get; set; } = string.Empty;
    public string BusinessUnitSetCode { get; set; } = string.Empty;
}

public sealed class CyclePeriodScopeOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>Why this option is in the list — for a business unit, the territory plans that cover it.</summary>
    public string? Hint { get; set; }
}

/// <summary>What the Index page needs to know before it renders: whether the actor may author, and whether the runtime
/// contract could be read at all.</summary>
public sealed class CyclePeriodIndexViewModel
{
    public bool CanManage { get; set; }
    public bool CanActivate { get; set; }
    public string? ContractError { get; set; }
}

/// <summary>The gateway envelope, mirrored so the proxy can read <c>data</c> / <c>errors</c> without a shared package.
/// </summary>
public sealed class CyclePeriodGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>The API's period detail, as much of it as the Edit / Details pages need.</summary>
public sealed class CyclePeriodDetailApiModel
{
    public Guid CyclePeriodId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeRef { get; set; }
    public string? CountryScope { get; set; }
    public Guid? LegalEntityId { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? BusinessUnitSource { get; set; }
    public string? BusinessUnitCountryContext { get; set; }
    public string? Description { get; set; }
    public string CycleStatus { get; set; } = string.Empty;
    public int Version { get; set; }
}

/// <summary>The scope-options payload as the API publishes it.</summary>
public sealed class CyclePeriodScopeOptionsApiModel
{
    public List<string> ScopeTypes { get; set; } = [];
    public List<CyclePeriodScopeOptionApiModel> Countries { get; set; } = [];
    public bool CountryReady { get; set; }
    public List<CyclePeriodScopeOptionApiModel> LegalEntities { get; set; } = [];
    public bool LegalEntityReady { get; set; }
    public List<CyclePeriodScopeOptionApiModel> BusinessUnits { get; set; } = [];
    public bool BusinessUnitReady { get; set; }
    public bool BusinessUnitFromTerritory { get; set; }
    public string CountrySetCode { get; set; } = string.Empty;
    public string BusinessUnitSetCode { get; set; } = string.Empty;
}

public sealed class CyclePeriodScopeOptionApiModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Hint { get; set; }
}
