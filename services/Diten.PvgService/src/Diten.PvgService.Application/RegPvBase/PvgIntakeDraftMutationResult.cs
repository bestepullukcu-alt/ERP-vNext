namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgIntakeDraftMutationResult(
    PvgApplicationResult Result,
    string? IntakeDraftId);
