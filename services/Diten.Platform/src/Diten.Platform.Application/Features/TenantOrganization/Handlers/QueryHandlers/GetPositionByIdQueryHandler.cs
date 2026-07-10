using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetPositionByIdQueryHandler : IRequestHandler<GetPositionByIdQuery, Response<PositionDto>>
{
    private readonly IPositionRepository _repository;
    private readonly IPositionAssignmentRepository _assignments;

    public GetPositionByIdQueryHandler(IPositionRepository repository, IPositionAssignmentRepository assignments)
    {
        _repository = repository;
        _assignments = assignments;
    }

    public async Task<Response<PositionDto>> Handle(GetPositionByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item == null)
        {
            return Response<PositionDto>.Fail("Position not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        var allAssignments = await _assignments.GetAllAsync(ct);
        var activeCount = allAssignments.Count(a => a.PositionId == item.Id && a.DeletedAt is null && TenantOrganizationMapper.IsActiveNow(a, now));

        return Response<PositionDto>.Success(TenantOrganizationMapper.ToDto(item, isVacant: activeCount == 0, activeAssignmentCount: activeCount));
    }
}
