using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-DM-DCP005-BL380-KURAL4-01 (Kural 4) — the SAME gate proven at the Mongo level
/// (<see cref="TaskTypeEffectivenessGateMongoTests"/>), now through the REAL <see cref="TasksController"/> action
/// for the wire-level shape: status code, <c>reason_code</c>, and — for the port-exception case — that a 503
/// really is what an unreachable register produces on the wire, not a generic 500.
/// </summary>
public sealed class TaskTypeEffectivenessGateHttpTests
{
    [Fact]
    public async Task PUT_active_true_on_a_type_with_a_blocked_document_returns_409_with_the_Kural4_reason_code()
    {
        var h = new Harness();
        var type = h.SeedInactiveTypeBoundTo("DOC-1");
        h.Port.Answers["DOC-1"] = DocumentEffectivenessState.Blocked;

        var result = await h.Controller.SetTaskTypeActive(type.Id, new SetTaskTypeActiveRequest(true, type.Version), CancellationToken.None);

        var obj = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(409, obj.StatusCode);
        var body = Assert.IsType<Response<NoContent>>(obj.Value);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableBlockedDocuments, body.ReasonCode);
    }

    [Fact]
    public async Task PUT_active_true_when_the_register_is_unreachable_returns_503_with_the_Kural4_reason_code()
    {
        var h = new Harness();
        var type = h.SeedInactiveTypeBoundTo("DOC-1");
        h.Port.Throws = true;

        var result = await h.Controller.SetTaskTypeActive(type.Id, new SetTaskTypeActiveRequest(true, type.Version), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        var body = Assert.IsType<Response<NoContent>>(obj.Value);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableRegisterUnavailable, body.ReasonCode);
    }

    [Fact]
    public async Task PUT_active_true_on_a_type_with_only_Effective_documents_returns_204()
    {
        var h = new Harness();
        var type = h.SeedInactiveTypeBoundTo("DOC-1");
        h.Port.Answers["DOC-1"] = DocumentEffectivenessState.Effective;

        var result = await h.Controller.SetTaskTypeActive(type.Id, new SetTaskTypeActiveRequest(true, type.Version), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PUT_active_false_on_a_type_with_a_blocked_document_is_never_gated_returns_204()
    {
        var h = new Harness();
        var type = h.SeedInactiveTypeBoundTo("DOC-1");
        type.IsActive = true;
        h.Port.Answers["DOC-1"] = DocumentEffectivenessState.Blocked;

        var result = await h.Controller.SetTaskTypeActive(type.Id, new SetTaskTypeActiveRequest(false, type.Version), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // ── harness ───────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        public FakeTaskTypeRepository Types { get; } = new();
        public FakeControlledDocumentEffectivenessPort Port { get; } = new();
        public TasksController Controller { get; }

        public Harness()
        {
            var setActive = new SetTaskTypeActiveHandler(Types, Port);
            var create = new CreateTaskTypeHandler(Types, new FakeTenantContext(TaskTestData.Tenant), new Mock<ICurrentUserContext>().Object, Port);
            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            Controller = new TasksController(new RoutingMediator(setActive, create), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public Domain.Entities.Tasks.TaskType SeedInactiveTypeBoundTo(string documentUid)
        {
            var type = new Domain.Entities.Tasks.TaskType
            {
                TenantId = TaskTestData.Tenant, Code = "GOV", Name = "Governed",
                IsActive = false, GroupDocuments = [documentUid]
            };
            Types.CreateAsync(type).GetAwaiter().GetResult();
            return type;
        }
    }

    private sealed class RoutingMediator(SetTaskTypeActiveHandler setActive, CreateTaskTypeHandler create) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object result = request switch
            {
                SetTaskTypeActiveCommand cmd => setActive.Handle(cmd, ct),
                CreateTaskTypeCommand cmd => create.Handle(cmd, ct),
                _ => throw new NotSupportedException($"RoutingMediator does not route {request.GetType().Name}.")
            };
            return (Task<TResponse>)result;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
