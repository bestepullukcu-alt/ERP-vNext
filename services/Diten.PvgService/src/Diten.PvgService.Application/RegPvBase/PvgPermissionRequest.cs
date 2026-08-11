using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgPermissionRequest(
    PvgIntakeOperation Operation,
    string RequiredPermission,
    string? TenantId,
    string? ActorId,
    string? CorrelationId);
