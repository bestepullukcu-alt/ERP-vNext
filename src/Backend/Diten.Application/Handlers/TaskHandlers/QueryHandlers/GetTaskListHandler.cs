using MediatR;
using Diten.Application.Queries.TaskQueries;
using Diten.Application.Common.Models;
using Diten.Application.Responses;
using Diten.Application.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Diten.Application.Handlers.TaskHandlers.QueryHandlers;

public class GetTaskListHandler : IRequestHandler<GetTaskListQuery, Response<List<TaskResponse>>>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskListHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Response<List<TaskResponse>>> Handle(GetTaskListQuery request, CancellationToken cancellationToken)
    {
        var tasks = await _taskRepository.GetAllAsync();

        var response = tasks.Select(t => new TaskResponse
        {
            Id = t.Id,
            Title = t.Title,
            Status = t.Status,
            CreatedDate = t.CreatedDate
        }).ToList();

        return Response<List<TaskResponse>>.Ok(response);
    }
}
