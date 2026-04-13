using MediatR;
using Diten.Application.Commands.TaskCommands;
using Diten.Application.Common.Models;
using Diten.Application.Repositories;
using Diten.Domain.Aggregates.Task;
using System.Threading;
using System.Threading.Tasks;

namespace Diten.Application.Handlers.TaskHandlers.CommandHandlers;

public class CreateTaskHandler : IRequestHandler<CreateTaskCommand, Response<string>>
{
    private readonly ITaskRepository _taskRepository;

    public CreateTaskHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Response<string>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new TaskAggregate
        {
            Title = request.Title,
            Description = request.Description,
            Status = request.Status
        };

        await _taskRepository.AddAsync(task);

        return Response<string>.Ok(task.Id, 201);
    }
}
