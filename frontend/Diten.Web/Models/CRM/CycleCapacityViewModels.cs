namespace Diten.Web.Models.CRM;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU06 — the READ-side view models and API mirrors. The create/edit form lives in
// CycleCapacityFormViewModels.cs, and the estimate projection in CycleCapacityCalculationViewModel.cs; see the note
// at the top of each for why the three are separate files.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>The pinned period as the form shows it. Projected on every read and never stored on the capacity.</summary>
public sealed class CycleCapacityPeriodViewModel
{
    public Guid CyclePeriodId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string CycleStatus { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeRef { get; set; }
    public string? CountryScope { get; set; }
    public bool IsClosed { get; set; }
}

/// <summary>One option of the period picker.</summary>
public sealed class CycleCapacityPeriodOptionViewModel
{
    public Guid CyclePeriodId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Hint { get; set; }
}

/// <summary>One governed country value.</summary>
public sealed class CycleCapacityCountryOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>What the Index page needs before it renders: whether the actor may author.</summary>
public sealed class CycleCapacityIndexViewModel
{
    public bool CanManage { get; set; }
}

/// <summary>The gateway envelope, mirrored so the proxy can read <c>data</c> / <c>errors</c> without a shared package.
/// </summary>
public sealed class CycleCapacityGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>The API's capacity detail, as much of it as the Edit / Details pages need.</summary>
public sealed class CycleCapacityDetailApiModel
{
    public Guid CycleCapacityId { get; set; }
    public Guid CyclePeriodId { get; set; }
    public CycleCapacityPeriodApiModel? CyclePeriod { get; set; }
    public string CalendarCountryCode { get; set; } = string.Empty;
    public bool CalendarCountryIsDerived { get; set; }
    public int DailyWorkMinutes { get; set; }
    public int PromoProductTime { get; set; }
    public int NonPromoProductTime { get; set; }
    public int TravelingTime { get; set; }
    public int ReportDuration { get; set; }
    public int QuizDuration { get; set; }
    public string? Description { get; set; }
    public List<CycleCapacityMonthApiModel> Months { get; set; } = [];
    public bool IsArchived { get; set; }
    public bool IsEditable { get; set; }
    public int Version { get; set; }
}

public sealed class CycleCapacityMonthApiModel
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public int MeetingDays { get; set; }
    public int TrainingDays { get; set; }
    public int VacationDays { get; set; }
    public int MicroTargetingDayCount { get; set; }
    public int MicroTargetingDuration { get; set; }

    /// <summary>FU07 — the month's own FTE, server-stamped.</summary>
    public decimal Fte { get; set; }

    public string FteSource { get; set; } = string.Empty;
}

public sealed class CycleCapacityPeriodApiModel
{
    public Guid CyclePeriodId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string CycleStatus { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeRef { get; set; }
    public string? CountryScope { get; set; }
    public bool IsClosed { get; set; }
}

/// <summary>The CyclePeriod selector payload, as the CyclePeriod API publishes it. Consumed READ-ONLY so the capacity
/// create page can offer a period picker without CyclePeriod knowing this module exists.</summary>
public sealed class CycleCapacityPeriodSelectorApiModel
{
    public List<CycleCapacityPeriodSelectorItemApiModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public sealed class CycleCapacityPeriodSelectorItemApiModel
{
    public Guid CyclePeriodId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string CycleStatus { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeRef { get; set; }
    public string? CountryScope { get; set; }
}

/// <summary>The capacity contract, as much of it as the form needs (the configured defaults a new capacity is born
/// with, so the create page shows the SAME numbers the server will write rather than hardcoding its own).</summary>
public sealed class CycleCapacityContractApiModel
{
    public CycleCapacityDefaultsApiModel? Defaults { get; set; }
}

public sealed class CycleCapacityDefaultsApiModel
{
    public int DailyWorkMinutes { get; set; }
    public decimal Fte { get; set; }
    public string FteSource { get; set; } = string.Empty;
    public bool FteIsEditable { get; set; }
    public string CountryReferenceSet { get; set; } = string.Empty;
}
