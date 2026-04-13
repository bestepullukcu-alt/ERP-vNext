using Diten.Application.Common.Models;
using Diten.Application.Queries.TaskQueries;
using Diten.Application.Repositories;
using Diten.Application.Responses;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Diten.Application.Handlers.TaskHandlers.QueryHandlers;

public class GetTaskReportSummaryHandler : IRequestHandler<GetTaskReportSummaryQuery, Response<TaskReportSummaryResponse>>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskReportSummaryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Response<TaskReportSummaryResponse>> Handle(GetTaskReportSummaryQuery request, CancellationToken cancellationToken)
    {
        var tasks = await _taskRepository.GetAllAsync();

        var totalCount = tasks.Count;
        var completedCount = tasks.Count(t => t.Status == "Completed");
        var inProgressCount = tasks.Count(t => t.Status == "InProgress");
        var pendingCount = tasks.Count(t => t.Status == "Pending");

        var completionRate = totalCount > 0 ? (double)completedCount / totalCount * 100 : 0;

        var response = new TaskReportSummaryResponse
        {
            TotalCount = totalCount,
            CompletedCount = completedCount,
            InProgressCount = inProgressCount,
            PendingCount = pendingCount,
            CompletionRate = Math.Round(completionRate, 1),
            ChartData = new List<int> { completedCount, inProgressCount, pendingCount },
            ChartLabels = new List<string> { "Completed", "In Progress", "Pending" }
        };

        return Response<TaskReportSummaryResponse>.Ok(response);
    }
}
