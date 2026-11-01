namespace Diten.PvgService.Domain.RegPvBase;

public sealed record PvgIntakeProductLine(
    string ProductRole,
    string? GlobalProductId,
    string ProductNameAsReported,
    string? PresentationGskuId,
    string? LotNumber);
