using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgApplicationSuccessMetadata(
    PvgIntakeOperation Operation,
    PvgIntakeStatus? Status,
    DateTimeOffset AcceptedAtUtc);
