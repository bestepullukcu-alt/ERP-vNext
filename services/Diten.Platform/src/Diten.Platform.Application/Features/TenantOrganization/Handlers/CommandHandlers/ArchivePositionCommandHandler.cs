using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class ArchivePositionCommandHandler : IRequestHandler<ArchivePositionCommand, Response<NoContent>>
{
    private readonly IPositionRepository _positions;

    public ArchivePositionCommandHandler(IPositionRepository positions)
    {
        _positions = positions;
    }

    public async Task<Response<NoContent>> Handle(ArchivePositionCommand request, CancellationToken ct)
    {
        var entity = await _positions.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Position not found.", 404);
        }

        if (entity.IsArchived)
        {
            return Response<NoContent>.Fail("Position is already archived.", 409);
        }

        entity.IsArchived = true;
        await _positions.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
