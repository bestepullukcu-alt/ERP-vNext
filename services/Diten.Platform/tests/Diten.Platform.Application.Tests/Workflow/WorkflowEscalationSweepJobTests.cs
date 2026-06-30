using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.BackgroundJobs;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

// A3 / MOD-0023 — the recurring escalation sweep must process EVERY active tenant in its own scope, and one
// tenant's failure must not abort the rest.
public sealed class WorkflowEscalationSweepJobTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Sweeps_every_active_tenant_in_its_own_scope()
    {
        var ctx = new TenantContext();
        var registry = new FakeTenantRegistryRepository(Tenant(TenantA), Tenant(TenantB));
        var mediator = new RecordingMediator(ctx);
        var job = new WorkflowEscalationSweepJob(registry, ctx, mediator, NullLogger<WorkflowEscalationSweepJob>.Instance);

        await job.HandleAsync(new WorkflowEscalationSweepJobArgs(100), Context(), CancellationToken.None);

        // One escalation run per tenant, each executed under that tenant's resolved context.
        Assert.Equal(new[] { TenantA, TenantB }, mediator.TenantsAtSend);
        // Context restored to unresolved after the sweep (no scope leak).
        Assert.False(ctx.IsResolved);
    }

    [Fact]
    public async Task One_tenant_failure_does_not_abort_the_others()
    {
        var ctx = new TenantContext();
        var registry = new FakeTenantRegistryRepository(Tenant(TenantA), Tenant(TenantB));
        var mediator = new RecordingMediator(ctx) { FailTenant = TenantA };
        var job = new WorkflowEscalationSweepJob(registry, ctx, mediator, NullLogger<WorkflowEscalationSweepJob>.Instance);

        await job.HandleAsync(new WorkflowEscalationSweepJobArgs(100), Context(), CancellationToken.None);

        // TenantA threw, TenantB still ran.
        Assert.Contains(TenantB, mediator.TenantsAtSend);
        Assert.False(ctx.IsResolved);
    }

    private static BackgroundJobContext Context() =>
        new(TriggerType: BackgroundJobTriggerTypes.Recurring, TriggeredBy: "test");

    private static Tenant Tenant(Guid id) => new()
    {
        Id = id,
        Code = $"T-{id:N}"[..10],
        Slug = $"t-{id:N}"[..10],
        Name = "Tenant",
        DisplayName = "Tenant",
        Domain = $"{id:N}.example",
        Status = TenantStatus.Active
    };

    private sealed class RecordingMediator(ITenantContext ctx) : IMediator
    {
        public List<Guid> TenantsAtSend { get; } = [];
        public Guid? FailTenant { get; init; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is RunWorkflowEscalationsCommand)
            {
                TenantsAtSend.Add(ctx.TenantId);
                if (FailTenant == ctx.TenantId)
                {
                    throw new InvalidOperationException("tenant escalation failed");
                }

                object resp = Response<RunWorkflowEscalationsResponse>.Success(
                    new RunWorkflowEscalationsResponse(0, 1, 0, 0, Array.Empty<WorkflowEscalationResultDto>(), "corr"));
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

    private sealed class FakeTenantRegistryRepository(params Tenant[] tenants) : ITenantRegistryRepository
    {
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(tenants.Where(t => t.Status == TenantStatus.Active).ToList());

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
