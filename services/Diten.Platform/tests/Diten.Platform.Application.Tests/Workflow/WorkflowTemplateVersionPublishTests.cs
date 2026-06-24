using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class WorkflowTemplateVersionPublishTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Correlation = "wf-publish-corr-001";

    [Fact]
    public async Task Draft_template_publish_creates_immutable_version_number_one_and_updates_active_pointer()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var template = await templates.CreateAsync(Template(ctx, "WF-PUB-1", WorkflowTemplateStatus.Draft));

        var response = await Handler(templates, versions, ctx).Handle(Publish(template.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(Correlation, response.CorrelationId);
        Assert.Equal(Correlation, response.Data!.CorrelationId);
        Assert.Equal(template.Id, response.Data.TemplateId);
        Assert.Equal(1, response.Data.VersionNumber);
        Assert.True(response.Data.IsImmutable);
        Assert.Equal("Published", response.Data.Status);

        var storedVersion = Assert.Single(versions.Items);
        Assert.True(storedVersion.IsImmutable);
        Assert.Equal(WorkflowTemplateVersionStatus.Published, storedVersion.Status);
        Assert.Equal(1, storedVersion.VersionNumber);
        Assert.NotNull(storedVersion.PublishedAt);
        Assert.Equal("test.actor@diten.local", storedVersion.PublishedBy);

        var reloadedTemplate = await templates.GetByIdAsync(template.Id);
        Assert.Equal(storedVersion.Id, reloadedTemplate!.ActivePublishedVersionId);
        Assert.Equal(storedVersion.Id, reloadedTemplate.CurrentVersionId);
        Assert.Equal(WorkflowTemplateStatus.Published, reloadedTemplate.Status);

        var reloadedVersion = await versions.GetByIdAsync(storedVersion.Id);
        Assert.Equal(storedVersion.Id, reloadedVersion!.Id);
    }

    [Fact]
    public async Task Second_publish_for_same_template_uses_monotonic_version_number_two()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var template = await templates.CreateAsync(Template(ctx, "WF-PUB-2", WorkflowTemplateStatus.Draft));

        var first = await Handler(templates, versions, ctx).Handle(Publish(template.Id), CancellationToken.None);
        var second = await Handler(templates, versions, ctx).Handle(Publish(template.Id), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(2, second.Data!.VersionNumber);
        Assert.Equal(second.Data.TemplateVersionId, (await templates.GetByIdAsync(template.Id))!.ActivePublishedVersionId);
        Assert.Equal(new[] { 1, 2 }, versions.Items.OrderBy(x => x.VersionNumber).Select(x => x.VersionNumber).ToArray());
    }

    [Fact]
    public async Task Published_version_mutation_attempt_is_blocked()
    {
        var ctx = TenantContextFor(TenantA);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var version = await versions.CreateAsync(new WorkflowTemplateVersion
        {
            TenantId = ctx.TenantId,
            TemplateId = Guid.NewGuid(),
            VersionNumber = 1,
            DefinitionJson = "{}",
            SchemaVersion = "1.0",
            ExpressionVersion = "1.0",
            Status = WorkflowTemplateVersionStatus.Published,
            IsImmutable = true,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = "publisher"
        });

        version.DefinitionJson = "{\"changed\":true}";
        var result = await versions.UpdateAsync(version, version.Version);

        Assert.Equal(WorkflowTemplateVersionUpdateResult.Immutable, result);
        Assert.Equal(WorkflowReasonCodes.WorkflowTemplateVersionImmutable, "WORKFLOW_TEMPLATE_VERSION_IMMUTABLE");
        Assert.Equal("{}", versions.Items.Single().DefinitionJson);
    }

    [Fact]
    public async Task Publish_missing_template_returns_non_leaking_not_found()
    {
        var ctx = TenantContextFor(TenantA);
        var response = await Handler(
                new FakeWorkflowTemplateRepository(ctx),
                new FakeWorkflowTemplateVersionRepository(ctx),
                ctx)
            .Handle(Publish(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_publish_is_blocked_with_not_found_non_leakage()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var template = await templates.CreateAsync(Template(ctx, "WF-XTENANT", WorkflowTemplateStatus.Draft));

        ctx.SetTenant(TenantB);
        var response = await Handler(templates, versions, ctx).Handle(Publish(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Empty(versions.Items);
    }

    [Fact]
    public async Task Tenant_a_template_cannot_be_published_by_tenant_b()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var tenantATemplate = await templates.CreateAsync(Template(ctx, "WF-TENANT-A", WorkflowTemplateStatus.Draft));

        ctx.SetTenant(TenantB);
        var tenantBAttempt = await Handler(templates, versions, ctx).Handle(Publish(tenantATemplate.Id), CancellationToken.None);

        Assert.False(tenantBAttempt.IsSuccessful);
        Assert.Equal(404, tenantBAttempt.StatusCode);
        Assert.Empty(versions.Items);
    }

    [Fact]
    public async Task Duplicate_version_number_same_tenant_template_is_blocked()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new DuplicateNextVersionRepository(ctx);
        var template = await templates.CreateAsync(Template(ctx, "WF-DUP-VERSION", WorkflowTemplateStatus.Draft));
        await versions.CreateAsync(new WorkflowTemplateVersion
        {
            TenantId = ctx.TenantId,
            TemplateId = template.Id,
            VersionNumber = 1,
            DefinitionJson = "{}",
            SchemaVersion = "1.0",
            ExpressionVersion = "1.0",
            Status = WorkflowTemplateVersionStatus.Published,
            IsImmutable = true
        });

        var response = await Handler(templates, versions, ctx).Handle(Publish(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTemplatePublishConflict, response.ReasonCode);
    }

    [Fact]
    public async Task Different_tenants_can_share_template_code_and_have_independent_version_numbers()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var tenantATemplate = await templates.CreateAsync(Template(ctx, "WF-SHARED", WorkflowTemplateStatus.Draft));
        var tenantAResult = await Handler(templates, versions, ctx).Handle(Publish(tenantATemplate.Id), CancellationToken.None);

        ctx.SetTenant(TenantB);
        var tenantBTemplate = await templates.CreateAsync(Template(ctx, "WF-SHARED", WorkflowTemplateStatus.Draft));
        var tenantBResult = await Handler(templates, versions, ctx).Handle(Publish(tenantBTemplate.Id), CancellationToken.None);

        Assert.True(tenantAResult.IsSuccessful);
        Assert.True(tenantBResult.IsSuccessful);
        Assert.Equal(1, tenantAResult.Data!.VersionNumber);
        Assert.Equal(1, tenantBResult.Data!.VersionNumber);
        Assert.Equal(2, versions.Items.Count);
    }

    [Fact]
    public void Publish_request_does_not_accept_tenant_id()
    {
        Assert.Null(typeof(PublishWorkflowDefinitionRequest).GetProperty("TenantId"));
    }

    [Fact]
    public async Task Version_queries_return_tenant_scoped_versions()
    {
        var ctx = TenantContextFor(TenantA);
        var templates = new FakeWorkflowTemplateRepository(ctx);
        var versions = new FakeWorkflowTemplateVersionRepository(ctx);
        var template = await templates.CreateAsync(Template(ctx, "WF-QUERY", WorkflowTemplateStatus.Draft));
        var published = await Handler(templates, versions, ctx).Handle(Publish(template.Id), CancellationToken.None);

        var list = await new GetWorkflowDefinitionVersionsHandler(templates, versions)
            .Handle(new GetWorkflowDefinitionVersionsQuery(template.Id, Correlation), CancellationToken.None);
        var detail = await new GetWorkflowDefinitionVersionByIdHandler(templates, versions)
            .Handle(new GetWorkflowDefinitionVersionByIdQuery(template.Id, published.Data!.TemplateVersionId, Correlation), CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(detail.IsSuccessful);
        Assert.Equal(published.Data.TemplateVersionId, detail.Data!.Id);
    }

    private static PublishWorkflowDefinitionCommand Publish(Guid templateId, int? expectedVersion = null) =>
        new(templateId, new PublishWorkflowDefinitionRequest(
            "{\"steps\":[{\"id\":\"review\"}]}",
            "1.0",
            "1.0",
            expectedVersion,
            null,
            "release"), Correlation);

    private static WorkflowTemplate Template(TenantContext ctx, string code, WorkflowTemplateStatus status) => new()
    {
        TenantId = ctx.TenantId,
        TemplateCode = code,
        Name = code,
        Status = status
    };

    private static TenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    private static PublishWorkflowDefinitionHandler Handler(
        IWorkflowTemplateRepository templates,
        IWorkflowTemplateVersionRepository versions,
        ITenantContext ctx) =>
        new(templates, versions, ctx, new FakeCurrentUserContext(), new FakeSlaEscalationRuleRepository(ctx));

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "test.actor@diten.local";
        public string? DisplayName => "Test Actor";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    private class FakeWorkflowTemplateRepository : IWorkflowTemplateRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTemplate> Items { get; } = [];

        public FakeWorkflowTemplateRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default)
        {
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

    private class FakeWorkflowTemplateVersionRepository : IWorkflowTemplateVersionRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTemplateVersion> Items { get; } = [];

        public FakeWorkflowTemplateVersionRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion version, CancellationToken ct = default)
        {
            typeof(WorkflowTemplateVersion).GetProperty(nameof(WorkflowTemplateVersion.TenantId))!
                .SetValue(version, _tenantContext.TenantId);
            Items.Add(Clone(version));
            return Task.FromResult(version);
        }

        public Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.TemplateId == templateId && x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted)
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefault());

        public async Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default) =>
            (await GetLatestVersionAsync(templateId, ct))?.VersionNumber ?? 0;

        public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x => x.TemplateId == templateId &&
                            x.TenantId == _tenantContext.TenantId &&
                            x.Status == WorkflowTemplateVersionStatus.Published &&
                            x.IsImmutable &&
                            !x.IsDeleted)
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefault());

        public virtual Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(x =>
                x.TemplateId == templateId &&
                x.VersionNumber == versionNumber &&
                x.TenantId == _tenantContext.TenantId &&
                !x.IsDeleted));

        public Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTemplateVersion>>(Items
                .Where(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted)
                .OrderByDescending(x => x.VersionNumber)
                .ToList());

        public Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(
            WorkflowTemplateVersion version,
            int expectedVersion,
            CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x =>
                x.Id == version.Id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted);
            if (stored is null || stored.Version != expectedVersion)
            {
                return Task.FromResult(WorkflowTemplateVersionUpdateResult.NotFoundOrConcurrencyConflict);
            }

            if (stored.IsImmutable || stored.Status == WorkflowTemplateVersionStatus.Published)
            {
                return Task.FromResult(WorkflowTemplateVersionUpdateResult.Immutable);
            }

            version.Version = expectedVersion + 1;
            Items[Items.IndexOf(stored)] = Clone(version);
            return Task.FromResult(WorkflowTemplateVersionUpdateResult.Updated);
        }

        private static WorkflowTemplateVersion Clone(WorkflowTemplateVersion source) => new()
        {
            Id = source.Id,
            TenantId = source.TenantId,
            CreatedAt = source.CreatedAt,
            CreatedBy = source.CreatedBy,
            TemplateId = source.TemplateId,
            VersionNumber = source.VersionNumber,
            DefinitionJson = source.DefinitionJson,
            SchemaVersion = source.SchemaVersion,
            ExpressionVersion = source.ExpressionVersion,
            Status = source.Status,
            IsImmutable = source.IsImmutable,
            PublishedAt = source.PublishedAt,
            PublishedBy = source.PublishedBy,
            PublishReason = source.PublishReason,
            ConcurrencyToken = source.ConcurrencyToken,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy,
            IsDeleted = source.IsDeleted,
            Version = source.Version
        };
    }

    private sealed class DuplicateNextVersionRepository : FakeWorkflowTemplateVersionRepository
    {
        public DuplicateNextVersionRepository(ITenantContext tenantContext) : base(tenantContext)
        {
        }

        public override Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeSlaEscalationRuleRepository : ISlaEscalationRuleRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<SlaEscalationRule> Items { get; } = [];

        public FakeSlaEscalationRuleRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<SlaEscalationRule> CreateAsync(SlaEscalationRule rule, CancellationToken ct = default)
        {
            typeof(SlaEscalationRule).GetProperty(nameof(SlaEscalationRule.TenantId))!
                .SetValue(rule, _tenantContext.TenantId);
            Items.Add(rule);
            return Task.FromResult(rule);
        }

        public Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlaEscalationRule>>(Items.Where(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && x.IsActive && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlaEscalationRule>>(Items.Where(x => x.TenantId == _tenantContext.TenantId && x.IsActive && !x.IsDeleted).ToList());

        public Task<SlaEscalationRule?> FindForStepAsync(Guid templateId, string stageCode, string stepCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.TemplateId == templateId && x.StageCode == stageCode && x.StepCode == stepCode && x.TenantId == _tenantContext.TenantId && x.IsActive && !x.IsDeleted));

        public Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default)
        {
            foreach (var rule in Items.Where(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && x.IsActive))
            {
                rule.IsActive = false;
                rule.DeletedAt = DateTimeOffset.UtcNow;
                rule.IsDeleted = true;
                rule.UpdatedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }
    }
}
