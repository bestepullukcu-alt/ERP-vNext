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
        try
        {
            await _repository.UpdateAsync(entity, ct);
        }
        catch (OrganizationUnitWriteConflictException)
        {
            // A re-parenting (or a delete) landed after the read; writing the stale copy back would undo it.
            return Response<NoContent>.Fail("The Organization Unit changed while it was being archived. Re-read it and try again.", 409);
        }

        return Response<NoContent>.Success(204);
    }
}
