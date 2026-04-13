using Diten.Application.Common.Models;
using Diten.Application.Responses;
using MediatR;

namespace Diten.Application.Queries.TaskQueries;

public class GetTaskReportSummaryQuery : IRequest<Response<TaskReportSummaryResponse>>
{
}
