using System.Security.Claims;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class WorkflowPublicActorBindingTests
{
    private static readonly Guid ActorId = Guid.Parse("31000000-0000-0000-0000-000000000031");

    [Theory]
    [InlineData("{\"actorId\":\"31000000-0000-0000-0000-000000000031\",\"reasonCode\":\"OK\",\"idempotencyKey\":\"key\"}")]
    [InlineData("{\"ActorId\":\"31000000-0000-0000-0000-000000000031\",\"reasonCode\":\"OK\",\"idempotencyKey\":\"key\"}")]
    [InlineData("{\"reasonCode\":\"OK\",\"reasonCode\":\"OTHER\",\"idempotencyKey\":\"key\"}")]
    [InlineData("{\"ReasonCode\":\"OK\",\"idempotencyKey\":\"key\"}")]
    public void External_transport_rejects_actor_unknown_duplicate_and_case_drift(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ApproveWorkflowTaskTransportRequest>(json));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<RejectWorkflowTaskTransportRequest>(json));
    }

    [Fact]
    public async Task Approve_binds_internal_actor_to_exact_authenticated_subject()
    {
        ApproveWorkflowTaskCommand? captured = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<ApproveWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>(
                (command, _) => captured = Assert.IsType<ApproveWorkflowTaskCommand>(command))
            .ReturnsAsync(Success());
        var controller = Controller(mediator.Object, Principal(ActorId));
        var taskId = Guid.NewGuid();

        var response = await controller.ApproveTask(
            taskId,
            new("APPROVE", "operation-1", "comment", null),
            CancellationToken.None);

        Assert.Equal(200, Assert.IsType<OkObjectResult>(response).StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(taskId, captured.TaskId);
        Assert.Equal(ActorId.ToString("D"), captured.Request.ActorId);
        Assert.Equal("APPROVE", captured.Request.ReasonCode);
    }

    [Fact]
    public async Task Reject_binds_internal_actor_to_exact_authenticated_subject()
    {
        RejectWorkflowTaskCommand? captured = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<RejectWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>(
                (command, _) => captured = Assert.IsType<RejectWorkflowTaskCommand>(command))
            .ReturnsAsync(Success());
        var controller = Controller(mediator.Object, Principal(ActorId));

        var response = await controller.RejectTask(
            Guid.NewGuid(),
            new("REJECT", "operation-2", null, "evidence"),
            CancellationToken.None);

        Assert.Equal(200, Assert.IsType<OkObjectResult>(response).StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(ActorId.ToString("D"), captured.Request.ActorId);
        Assert.Equal("REJECT", captured.Request.ReasonCode);
    }

    [Fact]
    public async Task Missing_malformed_or_duplicate_subject_fails_before_mediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var missing = Controller(mediator.Object, Principal());
        var malformed = Controller(mediator.Object, Principal(subject: "not-a-guid"));
        var duplicate = Controller(mediator.Object, Principal(
            ActorId,
            ActorId.ToString("D")));
        var request = new ApproveWorkflowTaskTransportRequest("APPROVE", "key", null, null);

        var missingResult = await missing.ApproveTask(Guid.NewGuid(), request, CancellationToken.None);
        var malformedResult = await malformed.ApproveTask(Guid.NewGuid(), request, CancellationToken.None);
        var duplicateResult = await duplicate.ApproveTask(Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(401, Assert.IsType<UnauthorizedObjectResult>(missingResult).StatusCode);
        Assert.Equal(401, Assert.IsType<UnauthorizedObjectResult>(malformedResult).StatusCode);
        Assert.Equal(401, Assert.IsType<UnauthorizedObjectResult>(duplicateResult).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    private static WorkflowDefinitionsController Controller(IMediator mediator, ClaimsPrincipal principal)
    {
        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(x => x.CorrelationId).Returns("correlation-1");
        return new WorkflowDefinitionsController(mediator, correlation.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static ClaimsPrincipal Principal(Guid? actorId = null, string? duplicate = null, string? subject = null)
    {
        var claims = new List<Claim>();
        if (actorId.HasValue || subject is not null)
        {
            claims.Add(new("sub", subject ?? actorId!.Value.ToString("D")));
        }

        if (duplicate is not null)
        {
            claims.Add(new("sub", duplicate));
        }

        return new(new ClaimsIdentity(claims, "Bearer"));
    }

    private static Response<WorkflowTaskTransitionResponse> Success() =>
        Response<WorkflowTaskTransitionResponse>.Success(new WorkflowTaskTransitionResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            "Approved",
            "Pending",
            "Approved",
            "Approve",
            false,
            Guid.NewGuid(),
            "correlation-1"));
}
