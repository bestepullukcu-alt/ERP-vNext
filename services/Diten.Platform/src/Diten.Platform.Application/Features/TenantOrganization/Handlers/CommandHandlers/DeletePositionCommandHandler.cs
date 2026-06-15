using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class DeletePositionCommandHandler : IRequestHandler<DeletePositionCommand, Response<NoContent>>
{
    private readonly IPositionRepository _positions;

    public DeletePositionCommandHandler(IPositionRepository positions)
    {
        _positions = positions;
    }

    public async Task<Response<NoContent>> Handle(DeletePositionCommand request, CancellationToken ct)
    {
        var entity = await _positions.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Position not found.", 404);
        }

        await _positions.DeleteAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
