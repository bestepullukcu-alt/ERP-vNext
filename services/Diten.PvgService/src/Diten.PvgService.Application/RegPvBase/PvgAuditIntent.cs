using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgAuditIntent(
    PvgIntakeOperation Operation,
    PvgIntakeStatus Status,
    string RequiredPermission,
    string ActorKind,
    bool HasCorrelation,
    DateTimeOffset AcceptedAtUtc);
