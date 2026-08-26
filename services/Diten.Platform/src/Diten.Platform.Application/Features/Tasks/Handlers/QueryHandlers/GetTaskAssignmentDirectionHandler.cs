using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// BL-023 — the create form's direction question, answered by the SAME rule the create handler applies when it
/// opens the request. One source, so the button's label and the server's behaviour cannot disagree.
/// </summary>
public sealed class GetTaskAssignmentDirectionHandler
    : IRequestHandler<GetTaskAssignmentDirectionQuery, Response<TaskAssignmentDirectionDto>>
{
    private readonly ITaskAssignmentDirection _direction;

    public GetTaskAssignmentDirectionHandler(ITaskAssignmentDirection direction) => _direction = direction;

    public async Task<Response<TaskAssignmentDirectionDto>> Handle(
        GetTaskAssignmentDirectionQuery request, CancellationToken ct)
        => Response<TaskAssignmentDirectionDto>.Success(
            new TaskAssignmentDirectionDto(await _direction.IsUpwardAsync(request.TargetUserId, ct)),
            correlationId: request.CorrelationId);
}
