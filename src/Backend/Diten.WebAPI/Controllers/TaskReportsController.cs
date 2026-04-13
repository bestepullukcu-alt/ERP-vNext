using System.Threading.Tasks;
using Asp.Versioning;
using Diten.Application.Commands.TaskCommands;
using Diten.Application.Queries.TaskQueries;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/task-reports")]
public class TaskReportsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetTasks()
    {
        var response = await Mediator.Send(new GetTaskListQuery());
        return CreateActionResultInstance(response);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var response = await Mediator.Send(new GetTaskReportSummaryQuery());
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var response = await Mediator.Send(new GetTaskByIdQuery(id));
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskCommand command)
    {
        var response = await Mediator.Send(command);
        return CreateActionResultInstance(response);
    }

    [HttpPut("status")]
    public async Task<IActionResult> UpdateStatus(UpdateTaskStatusCommand command)
    {
        var response = await Mediator.Send(command);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var response = await Mediator.Send(new DeleteTaskCommand(id));
        return CreateActionResultInstance(response);
    }
}
