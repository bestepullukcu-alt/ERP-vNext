using Diten.Application.Common.Models;
using Diten.Application.Responses;
using MediatR;

namespace Diten.Application.Queries.TaskQueries;

public class GetTaskByIdQuery : IRequest<Response<TaskResponse>>
{
    public string Id { get; set; } = string.Empty;

    public GetTaskByIdQuery(string id)
    {
        Id = id;
    }
}
