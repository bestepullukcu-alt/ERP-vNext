using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class ArchiveOrganizationUnitCommandHandler : IRequestHandler<ArchiveOrganizationUnitCommand, Response<NoContent>>
{
    private readonly IOrganizationUnitRepository _repository;

    public ArchiveOrganizationUnitCommandHandler(IOrganizationUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(ArchiveOrganizationUnitCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Organization Unit not found.", 404);
        }

        if (entity.IsArchived)
        {
            return Response<NoContent>.Fail("Organization Unit is already archived.", 409);
        }

        entity.IsArchived = true;
        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
