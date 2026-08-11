using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgValidationFailure(PvgIntakeField? Field, string ReasonCode);
