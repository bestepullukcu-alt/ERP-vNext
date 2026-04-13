using MediatR;
using Diten.Application.Common.Models;
using Diten.Application.Responses;
using System.Collections.Generic;

namespace Diten.Application.Queries.TaskQueries;

public class GetTaskListQuery : IRequest<Response<List<TaskResponse>>>
{
}
