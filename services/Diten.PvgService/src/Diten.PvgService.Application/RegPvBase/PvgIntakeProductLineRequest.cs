namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgIntakeProductLineRequest(
    string? ProductRole,
    string? GlobalProductId,
    string? ProductNameAsReported,
    string? PresentationGskuId,
    string? LotNumber);
