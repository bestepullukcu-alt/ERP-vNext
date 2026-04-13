using Diten.Application.Commands.TaskCommands;
using Diten.Application.Common.Models;
using Diten.Application.Repositories;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Diten.Application.Handlers.TaskHandlers.CommandHandlers;

public class DeleteTaskHandler : IRequestHandler<DeleteTaskCommand, Response<bool>>
{
    private readonly ITaskRepository _taskRepository;

    public DeleteTaskHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Response<bool>> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.Id);

        if (task == null)
        {
            return Response<bool>.Fail(ResultErrorCodes.NotFound, 404);
        }

        await _taskRepository.DeleteAsync(task);

        return Response<bool>.Ok(true);
    }
}
