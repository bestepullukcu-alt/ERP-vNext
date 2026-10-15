using System.Security.Claims;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Models.Workflow;
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

    private static readonly string[] AllActions = ["approve", "reject", "delegate", "request-info", "cancel"];

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

    /// <summary>
    /// WP-MVP1-SYNC1 — main's B4 (WP-WORKFLOW-APPROVAL-STATUS-01 v2) bound the actor with its own <c>TryBindActor</c>:
    /// accept a blank body actor, or one that <c>Guid.TryParse</c>s (after Trim) to the caller's id; refuse everything else
    /// with 403 <c>WORKFLOW_ACTOR_MISMATCH</c>. The merge keeps ONE binding, <see cref="WorkflowActorBinding"/>. These are
    /// the inputs main accepted, in every spelling <c>Guid.TryParse</c> takes: each one is accepted here and bound to the
    /// caller's id. (The only input accepted here that main refused is the caller's own e-mail — the case above.)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("c0000000-0000-0000-0000-0000000000c1")]
    [InlineData("C0000000-0000-0000-0000-0000000000C1")]
    [InlineData("c00000000000000000000000000000c1")]
    [InlineData("{c0000000-0000-0000-0000-0000000000c1}")]
    [InlineData("(c0000000-0000-0000-0000-0000000000c1)")]
    [InlineData("  c0000000-0000-0000-0000-0000000000c1  ")]
    public async Task Sync1_every_body_actor_mains_binding_accepted_is_accepted_and_bound_to_the_caller(string? bodyActor)
    {
        foreach (var action in AllActions)
        {
            var (mediator, sent) = CapturingMediator();
            var result = await Invoke(Controller(mediator), action, bodyActor!);

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(Caller.ToString(), Assert.Single(sent));
        }
    }

    /// <summary>WP-MVP1-SYNC1 — and the inputs main refused stay refused here (403, nothing sent).</summary>
    [Theory]
    [InlineData("c0000000-0000-0000-0000-0000000000c2")]
    [InlineData("user:c0000000-0000-0000-0000-0000000000c1")]
    [InlineData("not-a-person")]
    [InlineData("someone-else@tenant.test")]
    public async Task Sync1_every_body_actor_mains_binding_refused_other_than_the_own_email_is_refused(string bodyActor)
    {
        foreach (var action in AllActions)
        {
            var mediator = new Mock<IMediator>(MockBehavior.Strict);
            var result = await Invoke(Controller(mediator.Object), action, bodyActor);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
            Assert.Equal(WorkflowReasonCodes.WorkflowActorMismatch,
                Assert.IsType<Response<WorkflowTaskTransitionResponse>>(objectResult.Value).ReasonCode);
            mediator.VerifyNoOtherCalls();
        }
    }

    // Main sync 2026-10-13: still refused before anything is sent — with the lane's B4 answer for a token that names
    // no subject, 401 WORKFLOW_ACTOR_UNAUTHENTICATED (WorkflowPublicActorBindingTests), where main answered 403.
    [Fact]
    public async Task A_caller_without_a_user_id_cannot_act_on_a_task()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = Controller(mediator.Object, userId: Guid.Empty);

        var result = await Invoke(controller, "approve", "");

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    private static Task<IActionResult> Invoke(WorkflowDefinitionsController controller, string action, string actorId) => action switch
    {
        // Main sync 2026-10-13: approve / reject take the lane's strict transport bodies (WorkflowTaskTransitionTransportModels).
        "approve" => controller.ApproveTask(TaskId, new ApproveWorkflowTaskTransportRequest("APPROVED", "idem-1", null, null, ActorId: actorId), default),
        "reject" => controller.RejectTask(TaskId, new RejectWorkflowTaskTransportRequest("REJECTED", "idem-1", null, null, ActorId: actorId), default),
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
        // Main sync 2026-10-13: the lane's controller reads the caller from the TOKEN only (sub, and email for the e-mail rule).
        var controller = new WorkflowDefinitionsController(mediator, correlation.Object);
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
