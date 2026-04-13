using Diten.Application.Commands.TaskCommands;
using Diten.Application.Common.Models;
using Diten.Application.Repositories;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Diten.Application.Handlers.TaskHandlers.CommandHandlers;

public class UpdateTaskStatusHandler : IRequestHandler<UpdateTaskStatusCommand, Response<bool>>
{
    private readonly ITaskRepository _taskRepository;

    public UpdateTaskStatusHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Response<bool>> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.Id);

        if (task == null)
        {
            return Response<bool>.Fail(ResultErrorCodes.NotFound, 404);
        }

        task.Status = request.Status;
        task.LastModifiedDate = DateTime.UtcNow;

        await _taskRepository.UpdateAsync(task);

        return Response<bool>.Ok(true);
    }
}
