using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetPositionsQueryHandler : IRequestHandler<GetPositionsQuery, Response<IReadOnlyList<PositionDto>>>
{
    private readonly IPositionRepository _repository;
    private readonly IPositionAssignmentRepository _assignments;

    public GetPositionsQueryHandler(IPositionRepository repository, IPositionAssignmentRepository assignments)
    {
        _repository = repository;
        _assignments = assignments;
    }

    public async Task<Response<IReadOnlyList<PositionDto>>> Handle(GetPositionsQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(ct);

        // Derived occupancy: count currently-active assignments per position (see TenantOrganizationMapper.IsActiveNow).
        var now = DateTimeOffset.UtcNow;
        var allAssignments = await _assignments.GetAllAsync(ct);
        var activeByPosition = allAssignments
            .Where(a => a.DeletedAt is null && TenantOrganizationMapper.IsActiveNow(a, now))
            .GroupBy(a => a.PositionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var dtos = items
            .Select(p =>
            {
                var count = activeByPosition.TryGetValue(p.Id, out var c) ? c : 0;
                return TenantOrganizationMapper.ToDto(p, isVacant: count == 0, activeAssignmentCount: count);
            })
            .ToList();

        return Response<IReadOnlyList<PositionDto>>.Success(dtos);
    }
}
