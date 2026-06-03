using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetPositionAssignmentsQueryHandler : IRequestHandler<GetPositionAssignmentsQuery, Response<IReadOnlyList<PositionAssignmentDto>>>
{
    private readonly IPositionAssignmentRepository _repository;

    public GetPositionAssignmentsQueryHandler(IPositionAssignmentRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PositionAssignmentDto>>> Handle(GetPositionAssignmentsQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(ct);
        return Response<IReadOnlyList<PositionAssignmentDto>>.Success(items.Select(TenantOrganizationMapper.ToDto).ToList());
    }
}
