using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record GetIntakeDraftListQuery(
    PvgServerTenantContext TenantContext,
    PvgActorContext ActorContext,
    PvgCorrelationContext CorrelationContext,
    int PageNumber,
    int PageSize,
    PvgIntakeStatus? Status);
