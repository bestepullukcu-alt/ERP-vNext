using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.API.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class TrustedWorkflowStartAuthorizationTests
{
    [Fact]
    public async Task Denied_tuple_returns_403_before_coordinator_is_called()
    {
        var coordinator = new Mock<IWorkflowInstanceStartCoordinator>(MockBehavior.Strict);
        var handler = new StartTrustedWorkflowInstanceHandler(coordinator.Object, new FixedPolicy(false));

        var response = await handler.Handle(Command(), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal("WORKFLOW_TRUSTED_START_FORBIDDEN", response.ReasonCode);
        coordinator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Authorized_tuple_reaches_coordinator_with_unchanged_identity()
    {
        var expected = Response<TrustedWorkflowStartResult>.Success(null!, 201);
        var coordinator = new Mock<IWorkflowInstanceStartCoordinator>(MockBehavior.Strict);
        coordinator.Setup(value => value.StartAsync(
                It.IsAny<TrustedWorkflowStartRequest>(), ClientId, MakerId, "corr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var policy = new CapturingPolicy(true);
        var handler = new StartTrustedWorkflowInstanceHandler(coordinator.Object, policy);

        var response = await handler.Handle(Command(), default);

        Assert.Same(expected, response);
        Assert.Equal(new TrustedWorkflowStartAuthorizationRequest(
            ClientId,
            "Diten.MDM",
            "TRUSTED_WORKFLOW_CONSUMER",
            "GlobalProduct",
            TemplateId,
            null), policy.Captured);
        coordinator.VerifyAll();
    }

    [Fact]
    public async Task Controller_transports_exact_service_identity_and_selector_to_mediator_command()
    {
        StartTrustedWorkflowInstanceCommand? captured = null;
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(value => value.Send(
                It.IsAny<StartTrustedWorkflowInstanceCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((command, _) =>
                captured = Assert.IsType<StartTrustedWorkflowInstanceCommand>(command))
            .ReturnsAsync(Response<TrustedWorkflowStartResult>.Success(null!, 201));
        var executor = new DispatchingExecutor(
            new TrustedWorkflowStartTransportRequest(
                TemplateId, null, "GlobalProduct", "GP-1", null,
                ["maker"], "SUBMIT", true, false, null),
            new TrustedWorkflowConsumerServiceIdentity(
                ClientId, TenantId, Guid.NewGuid(), "Diten.MDM", "TRUSTED_WORKFLOW_CONSUMER"),
            new TrustedWorkflowDelegatedUserIdentity(MakerId, TenantId));
        var controller = new InternalTrustedWorkflowConsumerController(executor, mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-controller" }
            }
        };

        await controller.Start(default);

        Assert.NotNull(captured);
        Assert.Equal(ClientId, captured.ServiceClientId);
        Assert.Equal("Diten.MDM", captured.ServiceName);
        Assert.Equal("TRUSTED_WORKFLOW_CONSUMER", captured.ServiceAudience);
        Assert.Equal(MakerId, captured.DelegatedMakerUserId);
        Assert.Equal("GlobalProduct", captured.Request.ObjectType);
        Assert.Equal(TemplateId, captured.Request.TemplateId);
        Assert.Null(captured.Request.TemplateCode);
        Assert.Equal("transport-key", captured.Request.IdempotencyKey);
        Assert.Equal("trace-controller", captured.CorrelationId);
        mediator.VerifyAll();
    }

    private static StartTrustedWorkflowInstanceCommand Command() => new(
        new TrustedWorkflowStartRequest(
            TemplateId, null, "GlobalProduct", "GP-1", null, ["maker"], "SUBMIT",
            "start-1", true, false, null),
        ClientId,
        "Diten.MDM",
        "TRUSTED_WORKFLOW_CONSUMER",
        MakerId,
        "corr");

    private sealed class FixedPolicy(bool result) : ITrustedWorkflowStartAuthorizationPolicy
    {
        public bool IsAuthorized(TrustedWorkflowStartAuthorizationRequest request) => result;
    }

    private sealed class CapturingPolicy(bool result) : ITrustedWorkflowStartAuthorizationPolicy
    {
        public TrustedWorkflowStartAuthorizationRequest? Captured { get; private set; }

        public bool IsAuthorized(TrustedWorkflowStartAuthorizationRequest request)
        {
            Captured = request;
            return result;
        }
    }

    private sealed class DispatchingExecutor(
        TrustedWorkflowStartTransportRequest request,
        TrustedWorkflowConsumerServiceIdentity serviceIdentity,
        TrustedWorkflowDelegatedUserIdentity delegatedUser)
        : ITrustedWorkflowConsumerRequestExecutor
    {
        public Task<IActionResult> ExecuteStartAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken,
            Func<TrustedWorkflowStartTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
                TrustedWorkflowDelegatedUserIdentity, CancellationToken, Task<IActionResult>> dispatch,
            Func<int, string, IActionResult> failure) =>
            dispatch(request, "transport-key", serviceIdentity, delegatedUser, cancellationToken);

        public Task<IActionResult> ExecuteEvidenceAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken,
            Func<TrustedWorkflowTerminalDecisionEvidenceTransportRequest, TrustedWorkflowConsumerServiceIdentity,
                CancellationToken, Task<IActionResult>> dispatch,
            Func<int, string, IActionResult> failure) => throw new NotSupportedException();

        public Task<IActionResult> ExecuteStartResultAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken,
            Func<TrustedWorkflowStartResultTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
                CancellationToken, Task<IActionResult>> dispatch,
            Func<int, string, IActionResult> failure) => throw new NotSupportedException();
    }

    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MakerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TemplateId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
}
