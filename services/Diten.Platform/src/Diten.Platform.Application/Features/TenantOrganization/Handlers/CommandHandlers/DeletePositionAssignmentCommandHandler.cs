using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class DeletePositionAssignmentCommandHandler : IRequestHandler<DeletePositionAssignmentCommand, Response<NoContent>>
{
    private readonly IPositionAssignmentRepository _assignments;

    public DeletePositionAssignmentCommandHandler(IPositionAssignmentRepository assignments)
    {
        _assignments = assignments;
    }

    public async Task<Response<NoContent>> Handle(DeletePositionAssignmentCommand request, CancellationToken ct)
    {
        var entity = await _assignments.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Position Assignment not found.", 404);
        }

        await _assignments.DeleteAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
