using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

public sealed class GetUserLegalEntitiesQueryHandler : IRequestHandler<GetUserLegalEntitiesQuery, Response<IReadOnlyList<Guid>>>
{
    private readonly IUserLegalEntityAssignmentRepository _repository;

    public GetUserLegalEntitiesQueryHandler(IUserLegalEntityAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<Guid>>> Handle(GetUserLegalEntitiesQuery request, CancellationToken cancellationToken)
    {
        var assignments = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        IReadOnlyList<Guid> ids = assignments.Select(a => a.LegalEntityId).Distinct().ToList();
        return Response<IReadOnlyList<Guid>>.Success(ids);
    }
}
