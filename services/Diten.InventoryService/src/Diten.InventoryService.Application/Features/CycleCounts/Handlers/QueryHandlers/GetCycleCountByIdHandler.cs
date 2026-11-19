using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Queries;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.QueryHandlers;

/// <summary><c>getCycleCount</c>: a live count of the caller's company, else 404 <c>COUNT_NOT_FOUND</c>.</summary>
public sealed class GetCycleCountByIdHandler(ICycleCountRepository repository) : IRequestHandler<GetCycleCountByIdQuery, Response<CycleCountDto>>
{
    public async Task<Response<CycleCountDto>> Handle(GetCycleCountByIdQuery request, CancellationToken ct)
        => await repository.FindAsync(request.CountId, ct) is { } count
            ? Response<CycleCountDto>.Success(CycleCountReads.Dto(count))
            : CycleCountSupport.NotFound<CycleCountDto>();
}
