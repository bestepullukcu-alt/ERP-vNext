using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class DeleteOrganizationUnitCommandHandler : IRequestHandler<DeleteOrganizationUnitCommand, Response<NoContent>>
{
    private readonly IOrganizationUnitRepository _repository;

    public DeleteOrganizationUnitCommandHandler(IOrganizationUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteOrganizationUnitCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Organization Unit not found.", 404);
        }

        await _repository.DeleteAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
