using Diten.Web.Models.WorkCenter;
using Diten.Web.Services.WorkCenter;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TaskApiController : ControllerBase
{
    private readonly ITaskDetailService _taskDetailService;

    public TaskApiController(ITaskDetailService taskDetailService)
    {
        _taskDetailService = taskDetailService;
    }

    [HttpGet("{id}")]
    public IActionResult GetTaskDetail(string id, [FromQuery(Name = "as")] string? asUser)
    {
        var task = _taskDetailService.GetTaskDetail(id, ResolveUser(asUser));
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost("{id}/accept")]
    public IActionResult Accept(string id, [FromQuery(Name = "as")] string? asUser)
        => ToHttpResult(_taskDetailService.Accept(id, ResolveUser(asUser)));

    [HttpPost("{id}/plan")]
    public IActionResult Plan(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] PlanTaskRequest request)
        => ToHttpResult(_taskDetailService.Plan(id, ResolveUser(asUser), request));

    [HttpPost("{id}/start")]
    public IActionResult Start(string id, [FromQuery(Name = "as")] string? asUser)
        => ToHttpResult(_taskDetailService.Start(id, ResolveUser(asUser)));

    [HttpPost("{id}/log-time")]
    public IActionResult LogTime(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] LogTimeRequest request)
        => ToHttpResult(_taskDetailService.LogTime(id, ResolveUser(asUser), request));

    [HttpPost("{id}/request-info")]
    public IActionResult RequestInfo(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] RequestInfoRequest request)
        => ToHttpResult(_taskDetailService.RequestInfo(id, ResolveUser(asUser), request));

    [HttpPost("{id}/resume")]
    public IActionResult Resume(string id, [FromQuery(Name = "as")] string? asUser)
        => ToHttpResult(_taskDetailService.Resume(id, ResolveUser(asUser)));

    [HttpPost("{id}/complete")]
    public IActionResult Complete(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] CompleteTaskRequest request)
        => ToHttpResult(_taskDetailService.Complete(id, ResolveUser(asUser), request));

    [HttpPost("{id}/reassign")]
    public IActionResult Reassign(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] ReassignTaskRequest request)
        => ToHttpResult(_taskDetailService.Reassign(id, ResolveUser(asUser), request));

    [HttpPost("{id}/info-requests/{irId}/answer")]
    public IActionResult AnswerInfoRequest(string id, string irId, [FromQuery(Name = "as")] string? asUser, [FromBody] AnswerInfoRequest request)
        => ToHttpResult(_taskDetailService.AnswerInfoRequest(id, irId, ResolveUser(asUser), request));

    [HttpPost("{id}/cancel")]
    public IActionResult Cancel(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] CancelTaskRequest request)
        => ToHttpResult(_taskDetailService.Cancel(id, ResolveUser(asUser), request));

    [HttpPost("{id}/gates/approval/approve")]
    public IActionResult ApproveApprovalGate(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] GateDecisionRequest request)
        => ToHttpResult(_taskDetailService.ApproveApprovalGate(id, ResolveUser(asUser), request));

    [HttpPost("{id}/gates/approval/reject")]
    public IActionResult RejectApprovalGate(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] GateDecisionRequest request)
        => ToHttpResult(_taskDetailService.RejectApprovalGate(id, ResolveUser(asUser), request));

    [HttpPost("{id}/gates/review/approve")]
    public IActionResult ApproveReviewGate(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] GateDecisionRequest request)
        => ToHttpResult(_taskDetailService.ApproveReviewGate(id, ResolveUser(asUser), request));

    [HttpPost("{id}/gates/review/reject")]
    public IActionResult RejectReviewGate(string id, [FromQuery(Name = "as")] string? asUser, [FromBody] GateDecisionRequest request)
        => ToHttpResult(_taskDetailService.RejectReviewGate(id, ResolveUser(asUser), request));

    private string? ResolveUser(string? queryUser)
    {
        if (!string.IsNullOrWhiteSpace(queryUser))
            return queryUser;

        if (Request.Headers.TryGetValue("X-WorkCenter-User", out var headerUser) && !string.IsNullOrWhiteSpace(headerUser))
            return headerUser.ToString();

        return null;
    }

    private IActionResult ToHttpResult(TaskActionResult result)
    {
        var payload = new TaskActionResponse { Success = result.Success, Message = result.Message };
        return StatusCode(result.HttpStatusCode, payload);
    }
}
