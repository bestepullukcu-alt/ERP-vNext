using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgIntakeDraftSummary(
    string IntakeDraftId,
    PvgIntakeStatus Status);
