using System.Security.Claims;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-0023-FX — actor binding on the direct task API. The request DTOs carry an <c>ActorId</c> because they were
/// written for in-process callers (WorkCenter passes the JWT user id, the Tasks services the current user). Over
/// HTTP the body is caller-controlled, and the handlers only compare it with the assignment snapshot — so a holder of
/// the approve key could act as any other assignee by typing that person's id. The acting person must be the
/// authenticated caller: an empty body actor means "me", a different one is refused before anything is sent.
/// </summary>
public sealed class WorkflowActorBindingTests
{
    private static readonly Guid Caller = Guid.Parse("c0000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Victim = Guid.Parse("c0000000-0000-0000-0000-0000000000c2");
    private const string CallerEmail = "caller@tenant.test";
    private static readonly Guid TaskId = Guid.Parse("c0000000-0000-0000-0000-0000000000f1");

    public static TheoryData<string> Actions => new() { "approve", "reject", "delegate", "request-info", "cancel" };

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task A_body_actor_that_is_not_the_caller_is_refused_with_403_and_nothing_is_sent(string action)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = Controller(mediator.Object);

        var result = await Invoke(controller, action, Victim.ToString());

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var body = Assert.IsType<Response<WorkflowTaskTransitionResponse>>(objectResult.Value);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorMismatch, body.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_empty_body_actor_acts_as_the_caller(string action)
    {
        var (mediator, sent) = CapturingMediator();
        var result = await Invoke(Controller(mediator), action, "");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(Caller.ToString(), Assert.Single(sent));
    }

    [Theory]
    [InlineData("C0000000-0000-0000-0000-0000000000C1")]
    [InlineData("c0000000-0000-0000-0000-0000000000c1")]
    [InlineData("{c0000000-0000-0000-0000-0000000000c1}")]
    [InlineData(CallerEmail)]
    [InlineData("CALLER@tenant.test")]
    public async Task The_caller_named_by_id_or_own_email_is_accepted_and_normalised_to_the_user_id(string bodyActor)
    {
        var (mediator, sent) = CapturingMediator();
        var result = await Invoke(Controller(mediator), "approve", bodyActor);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(Caller.ToString(), Assert.Single(sent));
    }

    [Fact]
    public async Task A_caller_without_a_user_id_cannot_act_on_a_task()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = Controller(mediator.Object, userId: Guid.Empty);

        var result = await Invoke(controller, "approve", "");

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    private static Task<IActionResult> Invoke(WorkflowDefinitionsController controller, string action, string actorId) => action switch
    {
        "approve" => controller.ApproveTask(TaskId, new ApproveWorkflowTaskRequest(actorId, "APPROVED", "idem-1", null, null), default),
        "reject" => controller.RejectTask(TaskId, new RejectWorkflowTaskRequest(actorId, "REJECTED", "idem-1", null, null), default),
        "delegate" => controller.DelegateTask(TaskId, new DelegateWorkflowTaskRequest(actorId, "user:someone", "DELEGATED", "idem-1", null), default),
        "request-info" => controller.RequestInfoTask(TaskId, new RequestInfoWorkflowTaskRequest(actorId, null, "INFO", "idem-1", null, null), default),
        "cancel" => controller.CancelTask(TaskId, new CancelWorkflowTaskRequest(actorId, "CANCELLED", "idem-1", null), default),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (IMediator Mediator, List<string> SentActors) CapturingMediator()
    {
        var sent = new List<string>();
        var ok = Response<WorkflowTaskTransitionResponse>.Success(new WorkflowTaskTransitionResponse(
            Guid.NewGuid(), TaskId, "WaitingApproval", "Approved", "Active", "Completed", "Approve", false, Guid.NewGuid(), "corr"));
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ApproveWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>((c, _) => sent.Add(((ApproveWorkflowTaskCommand)c).Request.ActorId!))
            .ReturnsAsync(ok);
        mediator.Setup(m => m.Send(It.IsAny<RejectWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>((c, _) => sent.Add(((RejectWorkflowTaskCommand)c).Request.ActorId!))
            .ReturnsAsync(ok);
        mediator.Setup(m => m.Send(It.IsAny<DelegateWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>((c, _) => sent.Add(((DelegateWorkflowTaskCommand)c).Request.ActorId!))
            .ReturnsAsync(ok);
        mediator.Setup(m => m.Send(It.IsAny<RequestInfoWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>((c, _) => sent.Add(((RequestInfoWorkflowTaskCommand)c).Request.ActorId!))
            .ReturnsAsync(ok);
        mediator.Setup(m => m.Send(It.IsAny<CancelWorkflowTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Response<WorkflowTaskTransitionResponse>>, CancellationToken>((c, _) => sent.Add(((CancelWorkflowTaskCommand)c).Request.ActorId!))
            .ReturnsAsync(ok);
        return (mediator.Object, sent);
    }

    private static WorkflowDefinitionsController Controller(IMediator mediator, Guid? userId = null)
    {
        var id = userId ?? Caller;
        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(c => c.CorrelationId).Returns("corr");
        var controller = new WorkflowDefinitionsController(mediator, correlation.Object, new SessionUser(id, CallerEmail));
        var claims = id == Guid.Empty
            ? new List<Claim>()
            : new List<Claim> { new("sub", id.ToString()), new("email", CallerEmail) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private sealed class SessionUser(Guid userId, string email) : ICurrentUserContext
    {
        public Guid UserId => userId;
        public string? Email => userId == Guid.Empty ? null : email;
        public string? DisplayName => null;
        public string ActorName => Email ?? (userId == Guid.Empty ? "system" : userId.ToString());
        public bool IsAuthenticated => true;
    }
}
