using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgIntakeDraftQueryResult(
    PvgApplicationResult Result,
    IReadOnlyList<PvgIntakeDraftSummary> Items,
    int? TotalCount = null,
    int? FilteredCount = null,
    SafetyCaseIntake? Intake = null);
