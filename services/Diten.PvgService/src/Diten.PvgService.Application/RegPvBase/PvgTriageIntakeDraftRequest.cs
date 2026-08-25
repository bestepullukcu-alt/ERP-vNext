using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgTriageIntakeDraftRequest(
    PvgTriageOutcome? TriageOutcome,
    string? TriageReasonCode,
    string? TriageReason);
