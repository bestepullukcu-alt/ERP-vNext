using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

// A3-ref — the Module Catalog "activate" is the reference consumer of IWorkflowTransitionGate. These tests use
// the REAL gate wired to a fake mediator so the whole chain (handler → gate → evaluate → decision) is exercised.
public sealed class ModuleCatalogActivateGateTests
{
    [Fact]
    public async Task Activate_with_no_workflow_bound_is_not_applicable_and_succeeds()
    {
        var item = Draft();
        var repo = new FakeModuleCatalogRepository(item);
        // NotApplicable = no workflow attached to this object → gate allows → existing behaviour unchanged.
        var gate = new WorkflowTransitionGate(new GateMediator(WorkflowTransitionGateDecision.NotApplicable, WorkflowTransitionGateStatus.NoWorkflow), NullLogger<WorkflowTransitionGate>.Instance);
        var handler = new ActivateModuleCatalogItemCommandHandler(repo, gate);

        var response = await handler.Handle(new ActivateModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(ModuleCatalogStatus.Active, item.Status);
        Assert.Equal(1, repo.UpdateCount);
    }

    [Fact]
    public async Task Activate_blocked_by_gate_throws_and_leaves_item_in_draft_without_commit()
    {
        var item = Draft();
        var repo = new FakeModuleCatalogRepository(item);
        var gate = new WorkflowTransitionGate(new GateMediator(WorkflowTransitionGateDecision.Blocked, WorkflowTransitionGateStatus.PendingApproval, blockingReason: WorkflowReasonCodes.WorkflowPendingApproval), NullLogger<WorkflowTransitionGate>.Instance);
        var handler = new ActivateModuleCatalogItemCommandHandler(repo, gate);

        await Assert.ThrowsAsync<WorkflowTransitionBlockedException>(
            () => handler.Handle(new ActivateModuleCatalogItemCommand(item.Id), CancellationToken.None));

        // No commit: the item stays in Draft.
        Assert.Equal(ModuleCatalogStatus.Draft, item.Status);
        Assert.Equal(0, repo.UpdateCount);
    }

    private static ModuleCatalogItem Draft() => new()
    {
        Id = Guid.NewGuid(),
        ModuleCode = "MOD-REF",
        ModuleName = "Reference Module",
        DisplayName = "Reference Module",
        Status = ModuleCatalogStatus.Draft
    };

    internal sealed class GateMediator(
        WorkflowTransitionGateDecision decision,
        WorkflowTransitionGateStatus status,
        string? blockingReason = null) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is EvaluateWorkflowTransitionGateQuery q)
            {
                var data = new EvaluateWorkflowTransitionGateResponse(
                    decision,
                    status,
                    q.Request.ObjectType,
                    q.Request.ObjectId,
                    q.Request.ObjectRef,
                    null, null, null, null,
                    blockingReason,
                    blockingReason is null ? null : "Blocked by workflow.",
                    q.CorrelationId);
                object resp = Response<EvaluateWorkflowTransitionGateResponse>.Success(data, correlationId: q.CorrelationId);
                return Task.FromResult((TResponse)resp);
            }

            throw new NotSupportedException();
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }

    internal sealed class FakeModuleCatalogRepository(ModuleCatalogItem item) : IModuleCatalogRepository
    {
        public int UpdateCount { get; private set; }

        public Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(item.Id == id ? item : null);

        public Task UpdateAsync(ModuleCatalogItem updated, CancellationToken ct = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
