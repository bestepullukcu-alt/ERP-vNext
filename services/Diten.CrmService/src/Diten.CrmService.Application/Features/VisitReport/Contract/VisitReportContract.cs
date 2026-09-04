using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitReport.Contract;

/// <summary>
/// MOD-0155 FU02 contract surface: feature flags + in-domain vocabulary + supported filters + limits + error codes +
/// permissions + limitations. Published so a contract-driven UI needs no hardcoded vocabulary and no hardcoded ceiling —
/// a hardcoded list is a second source of truth, and it drifts silently. Outcome codes + sample/material types are NOT
/// in this vocabulary: they are reference-data-driven (MOD-0048, F-RD) and reach the UI from the published set, not here.
/// </summary>
public sealed record VisitReportContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    VisitReportFeatureFlags Features,
    VisitReportVocabularyDto Vocabularies,
    VisitReportSupportedFilters SupportedFilters,
    VisitReportContractLimits Limits,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>The in-domain vocabulary exactly as the runtime enforces it. Every dropdown is fed from here.</summary>
public sealed record VisitReportVocabularyDto(
    IReadOnlyList<string> ExecutionOutcomes,
    IReadOnlyList<string> ReportStatuses,
    IReadOnlyList<string> ReasonCodes)
{
    public static VisitReportVocabularyDto Current => new(
        VisitExecutionOutcome.All,
        VisitReportStatus.All,
        VisitReportReasonCodes.All);
}

/// <summary>Which list/calendar filters the runtime honours. A filter that is not here is not silently ignored.</summary>
public sealed record VisitReportSupportedFilters(IReadOnlyList<string> List)
{
    public static VisitReportSupportedFilters Current => new(new[]
    {
        "from", "to", "resourceId", "plannedVisitId", "reportStatus", "executionOutcome"
    });
}

/// <summary>Published ceilings, so the editor enforces the same numbers the runtime does.</summary>
public sealed record VisitReportContractLimits(
    int MaxResourceIdLength,
    int MaxOutcomeCodeLength,
    int MaxSampleItemTypeLength,
    int MaxFeedbackLength,
    int MaxNotesLength,
    int MaxReasonLength,
    int MaxSamples,
    int EditWindowMinutes)
{
    public static VisitReportContractLimits Current => new(
        VisitReportLimits.MaxResourceIdLength,
        VisitReportLimits.MaxOutcomeCodeLength,
        VisitReportLimits.MaxSampleItemTypeLength,
        VisitReportLimits.MaxFeedbackLength,
        VisitReportLimits.MaxNotesLength,
        VisitReportLimits.MaxReasonLength,
        VisitReportLimits.MaxSamples,
        VisitReportLimits.EditWindowMinutes);
}
