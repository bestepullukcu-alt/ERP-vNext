using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record GetIntakeDraftListQuery(
    PvgServerTenantContext TenantContext,
    int PageNumber,
    int PageSize,
    PvgIntakeStatus? Status);
