using Diten.Application.Common.Models;
using Diten.Application.Queries.TaskQueries;
using Diten.Application.Repositories;
using Diten.Application.Responses;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Diten.Application.Handlers.TaskHandlers.QueryHandlers;

public class GetTaskByIdHandler : IRequestHandler<GetTaskByIdQuery, Response<TaskResponse>>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskByIdHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Response<TaskResponse>> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.Id);

        if (task == null)
        {
            return Response<TaskResponse>.Fail(ResultErrorCodes.NotFound, 404);
        }

        var response = new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Status = task.Status,
            CreatedDate = task.CreatedDate
        };

        return Response<TaskResponse>.Ok(response);
    }
}
