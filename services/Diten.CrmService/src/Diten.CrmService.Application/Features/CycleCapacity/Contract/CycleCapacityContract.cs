using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CycleCapacity.Contract;

/// <summary>
/// MOD-0155 FU06 contract surface: feature flags + in-domain vocabulary + supported filters + limits + error codes +
/// permissions + limitations. Published so a contract-driven UI needs no hardcoded ceiling, no hardcoded resolution
/// name and no hardcoded reason code anywhere — a hardcoded list is a second source of truth, and it drifts silently.
/// </summary>
public sealed record CycleCapacityContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    CycleCapacityFeatureFlags Features,
    CycleCapacityVocabularyDto Vocabularies,
    CycleCapacitySupportedFilters SupportedFilters,
    CycleCapacityContractLimits Limits,
    CycleCapacityDefaultsDto Defaults,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>The in-domain vocabulary exactly as the runtime enforces it.</summary>
public sealed record CycleCapacityVocabularyDto(
    IReadOnlyList<string> Resolutions,
    IReadOnlyList<string> FteSources,
    IReadOnlyList<string> ReasonCodes)
{
    public static CycleCapacityVocabularyDto Current => new(
        CycleCapacityResolutions.All,
        CycleCapacityFteSources.All,
        CycleCapacityReasonCodes.All);
}

/// <summary>Which list filters the runtime actually honours. A filter that is not here is not silently ignored — a UI
/// can see it is unsupported instead of showing a control that does nothing.</summary>
public sealed record CycleCapacitySupportedFilters(IReadOnlyList<string> List)
{
    public static CycleCapacitySupportedFilters Current => new(new[]
    {
        "cyclePeriodId", "calendarCountryCode", "includeArchived", "search"
    });
}

/// <summary>Published ceilings, so the editor enforces the same numbers the runtime does.</summary>
public sealed record CycleCapacityContractLimits(
    int MaxMinutesPerDay,
    int MaxMinutesPerVisit,
    int MinDailyWorkMinutes,
    int MaxDailyWorkMinutes,
    int MinYear,
    int MaxYear,
    int MinMonthNumber,
    int MaxMonthNumber,
    int MaxDeductionDays,
    int MaxMonths,
    int MaxDescriptionLength,
    int CalendarCountryCodeLength)
{
    public static CycleCapacityContractLimits Current => new(
        CycleCapacityLimits.MaxMinutesPerDay,
        CycleCapacityLimits.MaxMinutesPerVisit,
        CycleCapacityLimits.MinDailyWorkMinutes,
        CycleCapacityLimits.MaxDailyWorkMinutes,
        CycleCapacityLimits.MinYear,
        CycleCapacityLimits.MaxYear,
        CycleCapacityLimits.MinMonthNumber,
        CycleCapacityLimits.MaxMonthNumber,
        CycleCapacityLimits.MaxDeductionDays,
        CycleCapacityLimits.MaxMonths,
        CycleCapacityLimits.MaxDescriptionLength,
        CycleCapacityLimits.CalendarCountryCodeLength);
}

/// <summary>
/// The configured values a new capacity is born with, published so the create form shows the SAME numbers the server
/// will write instead of hardcoding its own.
/// <para><see cref="Fte"/> is published together with <see cref="FteIsEditable"/> = false: the form renders it, states
/// where it came from, and disables it. The server ignores the payload's value regardless, so the flag is a UI hint
/// rather than the guard.</para>
/// </summary>
public sealed record CycleCapacityDefaultsDto(
    int DailyWorkMinutes,
    decimal Fte,
    string FteSource,
    bool FteIsEditable,
    string CountryReferenceSet);

/// <summary>Machine-readable refusal codes, so a UI and a smoke script can branch without parsing prose.</summary>
public static class CycleCapacityErrorCodes
{
    public static readonly IReadOnlyList<string> All = CycleCapacityReasonCodes.All;
}
