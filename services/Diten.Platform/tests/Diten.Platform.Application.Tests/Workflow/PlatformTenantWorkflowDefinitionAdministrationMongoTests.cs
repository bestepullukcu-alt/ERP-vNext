using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class PlatformTenantWorkflowDefinitionAdministrationMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
            "fu04_workflow_admin",
            SchemaProfile.Core,
            SchemaProfile.WorkflowWorkCenter);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Platform_admin_bridge_persists_and_publishes_in_target_tenant_with_cross_tenant_non_disclosure()
    {
        var tenantA = await Tenant(new TenantRegistryRepository(_harness.DbContext, _harness.TenantContext), "A");
        var tenantB = await Tenant(new TenantRegistryRepository(_harness.DbContext, _harness.TenantContext), "B");
        var templates = new WorkflowTemplateRepository(_harness.DbContext, _harness.TenantContext);
        var versions = new WorkflowTemplateVersionRepository(_harness.DbContext, _harness.TenantContext);
        var instances = new WorkflowInstanceRepository(_harness.DbContext, _harness.TenantContext);
        var tasks = new ApprovalTaskRepository(_harness.DbContext, _harness.TenantContext);
        var snapshots = new RuntimeAssignmentSnapshotRepository(_harness.DbContext, _harness.TenantContext);
        var logs = new WorkflowTransitionLogRepository(_harness.DbContext, _harness.TenantContext);
        var actor = new Actor();
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(
            new TenantRegistryRepository(_harness.DbContext, _harness.TenantContext), _harness.TenantContext);
        var http = Http();
        WorkflowDefinitionDetailDto? createdA = null;
        WorkflowDefinitionDetailDto? createdB = null;

        await executor.ExecuteAsync(http, tenantA.Id, CancellationToken.None, async token =>
        {
            var response = await new CreateWorkflowDefinitionHandler(templates, _harness.TenantContext, actor).Handle(
                Create("FU04-SHARED"), token);
            createdA = response.Data;
            return new OkResult();
        }, Failure);
        await executor.ExecuteAsync(http, tenantB.Id, CancellationToken.None, async token =>
        {
            var response = await new CreateWorkflowDefinitionHandler(templates, _harness.TenantContext, actor).Handle(
                Create("FU04-SHARED"), token);
            createdB = response.Data;
            return new OkResult();
        }, Failure);

        Assert.NotNull(createdA);
        Assert.NotNull(createdB);
        Assert.NotEqual(createdA!.Id, createdB!.Id);

        await executor.ExecuteAsync(http, tenantA.Id, CancellationToken.None, async token =>
        {
            var published = await new PublishWorkflowDefinitionHandler(
                templates, versions, _harness.TenantContext, actor).Handle(
                new PublishWorkflowDefinitionCommand(createdA.Id,
                    new PublishWorkflowDefinitionRequest(
                        "{\"steps\":[{\"id\":\"approve\"}]}", "1.0", "1.0", 1, null, "FU04"),
                    "fu04-mongo"), token);
            Assert.True(published.IsSuccessful);
            Assert.Equal(actor.ActorName, published.Data!.PublishedBy);
            return new OkResult();
        }, Failure);

        await executor.ExecuteAsync(http, tenantB.Id, CancellationToken.None, async token =>
        {
            var hidden = await new GetWorkflowDefinitionByIdHandler(templates).Handle(
                new GetWorkflowDefinitionByIdQuery(createdA.Id, "fu04-mongo"), token);
            Assert.False(hidden.IsSuccessful);
            Assert.Equal(404, hidden.StatusCode);
            return new OkResult();
        }, Failure);

        _harness.TenantContext.SetTenant(tenantA.Id);
        var stored = await templates.GetByIdAsync(createdA.Id);
        Assert.Equal(tenantA.Id, stored!.TenantId);
        Assert.Equal(actor.ActorName, stored.CreatedBy);
        Assert.Equal(actor.ActorName, stored.UpdatedBy);
        var storedVersion = Assert.Single(await versions.ListByTemplateIdAsync(createdA.Id));
        Assert.Equal(tenantA.Id, storedVersion.TenantId);
        Assert.True(storedVersion.IsImmutable);

        var coordinator = new WorkflowInstanceStartCoordinator(
            templates, versions, instances, tasks, snapshots, logs, _harness.TenantContext);
        var clientId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var started = await coordinator.StartAsync(
            Start(createdA.Id, "fu04-tenant-a-start"), clientId, makerId, "fu04-start-a");
        Assert.True(started.IsSuccessful);
        Assert.Equal(201, started.StatusCode);
        Assert.Equal(createdA.Id, started.Data!.TemplateId);
        Assert.Equal(storedVersion.Id, started.Data.TemplateVersionId);

        _harness.TenantContext.SetTenant(tenantB.Id);
        var tenantBHidden = await coordinator.StartAsync(
            Start(createdA.Id, "fu04-tenant-b-hidden"), clientId, makerId, "fu04-start-b");
        Assert.False(tenantBHidden.IsSuccessful);
        Assert.Equal(404, tenantBHidden.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, tenantBHidden.ReasonCode);
        Assert.Empty(await instances.GetAllForTenantAsync());

        _harness.TenantContext.SetTenant(SystemTenantRules.PlatformSystemTenantId);
        var systemTemplate = await CreatePublishedTemplateAsync(templates, versions, "FU04-SYSTEM");

        _harness.TenantContext.SetTenant(tenantA.Id);
        var systemHidden = await coordinator.StartAsync(
            Start(systemTemplate.Id, "fu04-system-hidden"), clientId, makerId, "fu04-start-system");
        Assert.False(systemHidden.IsSuccessful);
        Assert.Equal(404, systemHidden.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, systemHidden.ReasonCode);
        Assert.Single(await instances.GetAllForTenantAsync());
    }

    private static CreateWorkflowDefinitionCommand Create(string code) => new(
        new CreateWorkflowDefinitionRequest(code, code, null), "fu04-mongo");

    private static TrustedWorkflowStartRequest Start(Guid templateId, string idempotencyKey) => new(
        templateId,
        null,
        "GlobalProduct",
        "GP-FU04",
        null,
        ["approver-fu04"],
        "SUBMIT",
        idempotencyKey,
        true,
        false,
        DateTimeOffset.UtcNow.AddHours(1));

    private static async Task<WorkflowTemplate> CreatePublishedTemplateAsync(
        IWorkflowTemplateRepository templates,
        IWorkflowTemplateVersionRepository versions,
        string code)
    {
        var template = await templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = SystemTenantRules.PlatformSystemTenantId,
            TemplateCode = code,
            Name = code,
            Status = WorkflowTemplateStatus.Published
        });
        var version = await versions.CreateAsync(new WorkflowTemplateVersion
        {
            TenantId = SystemTenantRules.PlatformSystemTenantId,
            TemplateId = template.Id,
            VersionNumber = 1,
            DefinitionJson = "{}",
            SchemaVersion = "1.0",
            ExpressionVersion = "1.0",
            Status = WorkflowTemplateVersionStatus.Published,
            IsImmutable = true
        });
        template.ActivePublishedVersionId = version.Id;
        template.CurrentVersionId = version.Id;
        Assert.True(await templates.UpdateAsync(template, template.Version));
        return template;
    }

    private static async Task<Tenant> Tenant(ITenantRegistryRepository repository, string suffix) =>
        await repository.CreateAsync(new Tenant
        {
            Code = $"FU04-{suffix}-{Guid.NewGuid():N}",
            Slug = $"fu04-{suffix.ToLowerInvariant()}-{Guid.NewGuid():N}",
            Name = $"FU04 {suffix}", DisplayName = $"FU04 {suffix}",
            Domain = $"fu04-{Guid.NewGuid():N}.local", Status = TenantStatus.Active
        });

    private static DefaultHttpContext Http() => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("actor_type", "platform_admin"), new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D"))],
            "Bearer"))
    };

    private static IActionResult Failure(int status, string reason) => new ObjectResult(reason) { StatusCode = status };

    private sealed class Actor : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "workflow.admin@diten.local";
        public string? DisplayName => "Workflow Admin";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }
}
