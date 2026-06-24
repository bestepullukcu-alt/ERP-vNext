using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Validators;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

// MOD-0023 Batch 01 — data-foundation tests. Handlers run against a tenant-aware fake repository that
// mirrors the live TenantRepository<T> execution filter (tenant + IsDeleted), with a single shared
// ITenantContext (as in the runtime scoped request). Switching the context tenant simulates another
// request scope to prove cross-tenant isolation.
public sealed class WorkflowDefinitionFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Correlation = "wf-corr-001";

    [Fact]
    public async Task Create_persists_tenant_scoped_then_reloads_by_id()
    {
        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        var create = await CreateHandler(repo, ctx).Handle(
            Command("WF-APPROVAL-01", "Purchase Approval"), CancellationToken.None);

        Assert.True(create.IsSuccessful);
        Assert.Equal(201, create.StatusCode);
        Assert.Equal(Correlation, create.CorrelationId);
        Assert.Equal("Draft", create.Data!.Status);

        var stored = Assert.Single(repo.Items);
        Assert.Equal(TenantA, stored.TenantId);
        Assert.Equal(WorkflowTemplateStatus.Draft, stored.Status);

        var byId = await ByIdHandler(repo).Handle(
            new GetWorkflowDefinitionByIdQuery(create.Data.Id, Correlation), CancellationToken.None);

        Assert.True(byId.IsSuccessful);
        Assert.Equal(create.Data.Id, byId.Data!.Id);
        Assert.Equal("WF-APPROVAL-01", byId.Data.TemplateCode);
    }

    [Fact]
    public async Task List_returns_only_current_tenant_records()
    {
        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        await CreateHandler(repo, ctx).Handle(Command("WF-A-1", "A One"), CancellationToken.None);
        await CreateHandler(repo, ctx).Handle(Command("WF-A-2", "A Two"), CancellationToken.None);

        ctx.SetTenant(TenantB);
        await CreateHandler(repo, ctx).Handle(Command("WF-B-1", "B One"), CancellationToken.None);

        var listB = await ListHandler(repo).Handle(new GetWorkflowDefinitionListQuery(Correlation), CancellationToken.None);
        Assert.Single(listB.Data!);
        Assert.Equal("WF-B-1", listB.Data![0].TemplateCode);

        ctx.SetTenant(TenantA);
        var listA = await ListHandler(repo).Handle(new GetWorkflowDefinitionListQuery(Correlation), CancellationToken.None);
        Assert.Equal(2, listA.Data!.Count);
        Assert.DoesNotContain(listA.Data!, x => x.TemplateCode == "WF-B-1");
    }

    [Fact]
    public async Task Cross_tenant_read_returns_not_found_non_leakage()
    {
        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        var created = await CreateHandler(repo, ctx).Handle(Command("WF-SECRET", "Secret"), CancellationToken.None);

        // Switch to a different tenant scope and try to read tenant A's id.
        ctx.SetTenant(TenantB);
        var byId = await ByIdHandler(repo).Handle(
            new GetWorkflowDefinitionByIdQuery(created.Data!.Id, Correlation), CancellationToken.None);

        Assert.False(byId.IsSuccessful);
        Assert.Equal(404, byId.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, byId.ReasonCode);
        Assert.Null(byId.Data);
        // No leaked metadata: only the generic message, no template code/name echoed back.
        Assert.DoesNotContain(byId.Errors, e => e.Contains("WF-SECRET") || e.Contains("Secret"));
    }

    [Fact]
    public async Task Client_cannot_supply_tenant_id_resolved_from_context()
    {
        // The request contract has no TenantId member — a client cannot send one at all.
        Assert.Null(typeof(CreateWorkflowDefinitionRequest).GetProperty("TenantId"));

        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        await CreateHandler(repo, ctx).Handle(Command("WF-CTX", "Context Owned"), CancellationToken.None);

        // Persisted tenant comes from the server-side context, not any client input.
        Assert.Equal(TenantA, Assert.Single(repo.Items).TenantId);
    }

    [Fact]
    public async Task Duplicate_template_code_in_same_tenant_is_blocked()
    {
        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        var first = await CreateHandler(repo, ctx).Handle(Command("WF-DUP", "First"), CancellationToken.None);
        var second = await CreateHandler(repo, ctx).Handle(Command("WF-DUP", "Second"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(WorkflowReasonCodes.DuplicateTemplateCode, second.ReasonCode);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task Same_template_code_in_different_tenant_is_allowed()
    {
        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        var inA = await CreateHandler(repo, ctx).Handle(Command("WF-SHARED", "In A"), CancellationToken.None);

        ctx.SetTenant(TenantB);
        var inB = await CreateHandler(repo, ctx).Handle(Command("WF-SHARED", "In B"), CancellationToken.None);

        Assert.True(inA.IsSuccessful);
        Assert.True(inB.IsSuccessful);
        Assert.Equal(2, repo.Items.Count);
        Assert.Contains(repo.Items, x => x.TenantId == TenantA && x.TemplateCode == "WF-SHARED");
        Assert.Contains(repo.Items, x => x.TenantId == TenantB && x.TemplateCode == "WF-SHARED");
    }

    [Fact]
    public async Task Soft_deleted_record_is_not_listed_or_readable()
    {
        var ctx = TenantContextFor(TenantA);
        var repo = new FakeWorkflowTemplateRepository(ctx);

        var created = await CreateHandler(repo, ctx).Handle(Command("WF-DEL", "To Delete"), CancellationToken.None);
        repo.Items.Single().IsDeleted = true; // simulate soft delete

        var list = await ListHandler(repo).Handle(new GetWorkflowDefinitionListQuery(Correlation), CancellationToken.None);
        Assert.Empty(list.Data!);

        var byId = await ByIdHandler(repo).Handle(
            new GetWorkflowDefinitionByIdQuery(created.Data!.Id, Correlation), CancellationToken.None);
        Assert.False(byId.IsSuccessful);
        Assert.Equal(404, byId.StatusCode);
    }

    [Fact]
    public void Validator_rejects_empty_code_and_name()
    {
        var validator = new CreateWorkflowDefinitionValidator();

        var bad = validator.Validate(new CreateWorkflowDefinitionCommand(
            new CreateWorkflowDefinitionRequest("", "", null), Correlation));
        Assert.False(bad.IsValid);

        var good = validator.Validate(new CreateWorkflowDefinitionCommand(
            new CreateWorkflowDefinitionRequest("WF-OK", "Ok", null), Correlation));
        Assert.True(good.IsValid);
    }

    private static CreateWorkflowDefinitionCommand Command(string code, string name) =>
        new(new CreateWorkflowDefinitionRequest(code, name, null), Correlation);

    private static TenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    private static CreateWorkflowDefinitionHandler CreateHandler(IWorkflowTemplateRepository repo, ITenantContext ctx) =>
        new(repo, ctx);

    private static GetWorkflowDefinitionByIdHandler ByIdHandler(IWorkflowTemplateRepository repo) => new(repo);

    private static GetWorkflowDefinitionListHandler ListHandler(IWorkflowTemplateRepository repo) => new(repo);

    // Tenant-aware fake mirroring the live TenantRepository<T> execution filter (tenant + IsDeleted).
    private sealed class FakeWorkflowTemplateRepository : IWorkflowTemplateRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTemplate> Items { get; } = [];

        public FakeWorkflowTemplateRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default)
        {
            // Re-assert tenant from context (as the live base repository does).
            typeof(WorkflowTemplate).GetProperty(nameof(WorkflowTemplate.TenantId))!
                .SetValue(template, _tenantContext.TenantId);
            Items.Add(template);
            return Task.FromResult(template);
        }

        public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.TemplateCode == templateCode && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTemplate>>(Items
                .Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted)
                .ToList());

        public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x =>
                x.Id == template.Id &&
                x.TenantId == _tenantContext.TenantId &&
                !x.IsDeleted &&
                x.Version == expectedVersion);
            if (stored is null)
            {
                return Task.FromResult(false);
            }

            template.Version = expectedVersion + 1;
            template.UpdatedAt = DateTimeOffset.UtcNow;
            Items[Items.IndexOf(stored)] = template;
            return Task.FromResult(true);
        }
    }
}
