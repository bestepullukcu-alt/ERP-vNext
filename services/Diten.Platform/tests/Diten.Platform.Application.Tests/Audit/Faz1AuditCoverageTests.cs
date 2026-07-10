using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

// MOD-0021 FAZ 1 — verifies the self-declared central-audit metadata for the platform commands newly brought under
// the AuditBehavior pipeline (TenantOrganization Position/Assignment, Quotas, SubscriptionPlans, SubscriptionFeatures).
// Request params that GetAuditMetadata does not read are passed as null! (metadata is derived from the command's own ids).
public sealed class Faz1AuditCoverageTests
{
    private static AuditRequestMetadata Meta(object command)
    {
        Assert.IsAssignableFrom<IAuditableCommand>(command);
        return ((IAuditMetadataProvider)command).GetAuditMetadata();
    }

    [Fact]
    public void Position_create_is_tenant_administration()
    {
        var m = Meta(new CreatePositionCommand(null!));
        Assert.Equal(AuditCategory.TenantAdministration, m.Category);
        Assert.Equal(AuditOperation.Create, m.Operation);
        Assert.Equal("Position", m.EntityType);
        Assert.Equal("organization", m.SourceModule);
    }

    [Fact]
    public void Position_archive_maps_to_deactivate_and_carries_id()
    {
        var id = Guid.NewGuid();
        var m = Meta(new ArchivePositionCommand(id));
        Assert.Equal(AuditCategory.TenantAdministration, m.Category);
        Assert.Equal(AuditOperation.Deactivate, m.Operation);
        Assert.Equal("Position", m.EntityType);
        Assert.Equal(id, m.EntityId);
    }

    [Fact]
    public void PositionAssignment_delete_carries_id()
    {
        var id = Guid.NewGuid();
        var m = Meta(new DeletePositionAssignmentCommand(id));
        Assert.Equal(AuditCategory.TenantAdministration, m.Category);
        Assert.Equal(AuditOperation.Delete, m.Operation);
        Assert.Equal("PositionAssignment", m.EntityType);
        Assert.Equal(id, m.EntityId);
    }

    [Fact]
    public void Quota_initialize_is_quota_category_scoped_to_tenant()
    {
        var tenantId = Guid.NewGuid();
        var m = Meta(new InitializeTenantQuotasCommand(tenantId, null!));
        Assert.Equal(AuditCategory.Quota, m.Category);
        Assert.Equal(AuditOperation.Create, m.Operation);
        Assert.Equal("Quota", m.EntityType);
        Assert.Equal("quotas", m.SourceModule);
        Assert.Equal(tenantId, m.TargetTenantId);
    }

    [Fact]
    public void Quota_consume_is_execute_operation()
    {
        var m = Meta(new TryConsumeQuotaCommand(null!));
        Assert.Equal(AuditCategory.Quota, m.Category);
        Assert.Equal(AuditOperation.Execute, m.Operation);
        Assert.Equal("Quota", m.EntityType);
    }

    [Fact]
    public void SubscriptionPlan_activate_is_billing_platform_global()
    {
        var id = Guid.NewGuid();
        var m = Meta(new ActivateSubscriptionPlanCommand(id));
        Assert.Equal(AuditCategory.SubscriptionBilling, m.Category);
        Assert.Equal(AuditOperation.Activate, m.Operation);
        Assert.Equal("SubscriptionPlan", m.EntityType);
        Assert.Equal(id, m.EntityId);
        Assert.True(m.IsPlatformGlobal);
    }

    [Fact]
    public void SubscriptionPlan_seed_is_recorded_under_system()
    {
        var m = Meta(new SeedDefaultSubscriptionPlansCommand());
        Assert.Equal(AuditCategory.System, m.Category);
        Assert.Equal(AuditOperation.Create, m.Operation);
        Assert.Equal("SubscriptionPlan", m.EntityType);
    }

    [Fact]
    public void FeatureCategory_archive_maps_to_deactivate()
    {
        var id = Guid.NewGuid();
        var m = Meta(new ArchiveFeatureCategoryCommand(id, null));
        Assert.Equal(AuditCategory.SubscriptionBilling, m.Category);
        Assert.Equal(AuditOperation.Deactivate, m.Operation);
        Assert.Equal("FeatureCategory", m.EntityType);
        Assert.Equal(id, m.EntityId);
    }

    [Fact]
    public void PlanFeatureMappings_update_anchors_entity_to_plan()
    {
        var planId = Guid.NewGuid();
        var m = Meta(new UpdatePlanFeatureMappingsCommand(planId, null!));
        Assert.Equal(AuditCategory.SubscriptionBilling, m.Category);
        Assert.Equal(AuditOperation.Update, m.Operation);
        Assert.Equal("PlanFeatureMapping", m.EntityType);
        Assert.Equal(planId, m.EntityId);
    }

    // End-to-end through the pipeline: a newly-instrumented command actually produces an AuditEvent append.
    [Fact]
    public async Task InstrumentedCommand_flowsThroughAuditBehavior_AppendsAuditEvent()
    {
        var auditService = new CapturingAuditService();
        var behavior = new AuditBehavior<UpdatePositionCommand, Response<NoContent>>(
            auditService,
            new AuditBehaviorOptions(),
            NullLogger<AuditBehavior<UpdatePositionCommand, Response<NoContent>>>.Instance);

        var positionId = Guid.NewGuid();
        var response = await behavior.Handle(
            new UpdatePositionCommand(positionId, null!),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var appended = Assert.Single(auditService.Requests);
        Assert.Equal(AuditOutcome.Succeeded, appended.Outcome);
        Assert.Equal(AuditCategory.TenantAdministration, appended.Category);
        Assert.Equal(AuditOperation.Update, appended.Operation);
        Assert.Equal("Position", appended.EntityType);
        Assert.Equal(positionId, appended.EntityId);
        Assert.Equal(nameof(UpdatePositionCommand), appended.RequestType);
    }

    private sealed class CapturingAuditService : IAuditService
    {
        public List<AuditAppendRequest> Requests { get; } = [];

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(AuditAppendResult.Queued("audit:test"));
        }
    }
}
