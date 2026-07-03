using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

// FIX-AUDIT-BIZ-REJECTED — the instrumented business-critical commands were being SILENTLY REJECTED by
// AuditService.AppendAsync: a command that is neither IsPlatformGlobal nor tenant-context-resolved cannot resolve
// an owning tenant (platform-admin requests never resolve a tenant), so ResolveTenantId returned Rejected and the
// event never reached the outbox. These tests lock the contract that closes that gap:
//   • group A (platform-global governance) MUST self-declare IsPlatformGlobal:true, and its append must be
//     ACCEPTED (Queued, not Rejected) even with NO tenant context — mirroring the real platform-admin request.
//   • group B (tenant-scoped) MUST carry TargetTenantId, and its append must be ACCEPTED under a platform context
//     pinned to the target tenant.
public sealed class BizCriticalAuditRejectionTests
{
    // Every group-A command that gained IsPlatformGlobal:true. Requests that GetAuditMetadata does not read are
    // passed as null! — the metadata derives from the command's own ids/counts.
    public static IEnumerable<object[]> PlatformGlobalCommands()
    {
        yield return new object[] { new RegisterTenantCommand("Acme", "acme.example") };
        yield return new object[] { new BulkDeleteTenantsCommand(new[] { Guid.NewGuid() }) };
        yield return new object[] { new AssignPlatformAdministratorRolesCommand(Guid.NewGuid(), null!) };
        yield return new object[] { new UpdatePlatformAdministratorCommand(Guid.NewGuid(), null!) };
        yield return new object[] { new DeletePlatformAdministratorCommand(Guid.NewGuid(), null!) };
        yield return new object[] { new BulkDeletePlatformAdministratorsCommand(Array.Empty<PlatformAdministratorBulkDeleteItemRequest>()) };
        yield return new object[] { new ReactivatePlatformAdministratorCommand(Guid.NewGuid(), null!) };
        yield return new object[] { new CreateModuleCatalogItemCommand(SampleCreateRequest()) };
        yield return new object[] { new UpdateModuleCatalogItemCommand(Guid.NewGuid(), SampleUpdateRequest()) };
        yield return new object[] { new DeleteModuleCatalogItemCommand(Guid.NewGuid()) };
        yield return new object[] { new BulkDeleteModuleCatalogItemsCommand(new[] { Guid.NewGuid() }) };
        yield return new object[] { new ActivateModuleCatalogItemCommand(Guid.NewGuid()) };
        yield return new object[] { new DeactivateModuleCatalogItemCommand(Guid.NewGuid()) };
    }

    [Theory]
    [MemberData(nameof(PlatformGlobalCommands))]
    public void GroupA_declares_platform_global(IAuditableCommand command)
    {
        var metadata = ((IAuditMetadataProvider)command).GetAuditMetadata();

        Assert.True(
            metadata.IsPlatformGlobal,
            $"{command.GetType().Name} is platform-global and must set IsPlatformGlobal:true, otherwise its audit " +
            "append is rejected when no tenant context resolves.");
        Assert.Null(metadata.TargetTenantId);
    }

    [Theory]
    [MemberData(nameof(PlatformGlobalCommands))]
    public async Task GroupA_append_is_accepted_without_tenant_context(IAuditableCommand command)
    {
        var writer = new CapturingWriter();
        // No tenant context is resolved — e.g. background/system execution outside a request pipeline.
        var service = CreateService(writer, new TenantContext());

        var result = await service.AppendAsync(ToAppendRequest(command));

        Assert.NotEqual(AuditAppendStatus.Rejected, result.Status);
        Assert.Equal(AuditAppendStatus.Queued, result.Status);
        Assert.NotNull(writer.Request);
        Assert.Equal(Diten.Platform.Domain.Entities.Audit.AuditTenantIds.PlatformSystemTenantId, writer.Request!.TenantId);
    }

    [Theory]
    [MemberData(nameof(PlatformGlobalCommands))]
    public async Task GroupA_append_is_accepted_under_real_platform_admin_context(IAuditableCommand command)
    {
        var writer = new CapturingWriter();
        // FIX-AUDIT-TARGET-EMPTY — the REAL middleware semantics: TenantResolutionMiddleware pins admin-path
        // requests with SetPlatformContext(Guid.Empty) → IsResolved=true, IsPlatformContext=true,
        // TargetTenantId=Guid.Empty. That Guid.Empty sentinel used to leak into ResolveTargetTenantId and hit
        // the "target tenant id cannot be empty" rejection.
        var tenantContext = new TenantContext();
        tenantContext.SetPlatformContext(Guid.Empty);
        var service = CreateService(writer, tenantContext);

        var result = await service.AppendAsync(ToAppendRequest(command));

        Assert.NotEqual(AuditAppendStatus.Rejected, result.Status);
        Assert.Equal(AuditAppendStatus.Queued, result.Status);
        Assert.NotNull(writer.Request);
        Assert.Equal(Diten.Platform.Domain.Entities.Audit.AuditTenantIds.PlatformSystemTenantId, writer.Request!.TenantId);
    }

    [Fact]
    public async Task Explicit_empty_target_tenant_is_still_rejected()
    {
        var writer = new CapturingWriter();
        var tenantContext = new TenantContext();
        tenantContext.SetPlatformContext(Guid.Empty);
        var service = CreateService(writer, tenantContext);

        // An explicitly declared Guid.Empty target is intentional misuse — the guard must keep rejecting it;
        // only the middleware's context sentinel is normalized to "no target".
        var request = ToAppendRequest(new DeleteModuleCatalogItemCommand(Guid.NewGuid())) with
        {
            TargetTenantId = Guid.Empty
        };

        var result = await service.AppendAsync(request);

        Assert.Equal(AuditAppendStatus.Rejected, result.Status);
        Assert.Null(writer.Request);
    }

    // Every group-B command already carried TargetTenantId; representatives across the tenant-scoped categories.
    public static IEnumerable<object[]> TenantScopedCommands()
    {
        var tenantId = Guid.NewGuid();
        yield return new object[] { new DeleteTenantCommand(tenantId), tenantId };
        yield return new object[] { new ReactivateTenantCommand(tenantId), tenantId };
        yield return new object[] { new DeleteTenantAdminUserCommand(tenantId, Guid.NewGuid()), tenantId };
    }

    [Theory]
    [MemberData(nameof(TenantScopedCommands))]
    public void GroupB_declares_target_tenant(IAuditableCommand command, Guid expectedTenantId)
    {
        var metadata = ((IAuditMetadataProvider)command).GetAuditMetadata();

        Assert.False(metadata.IsPlatformGlobal);
        Assert.NotNull(metadata.TargetTenantId);
        Assert.Equal(expectedTenantId, metadata.TargetTenantId);
    }

    [Theory]
    [MemberData(nameof(TenantScopedCommands))]
    public async Task GroupB_append_is_accepted_under_real_platform_admin_context(IAuditableCommand command, Guid expectedTenantId)
    {
        var writer = new CapturingWriter();
        // FIX-AUDIT-TARGET-EMPTY — real middleware semantics again (SetPlatformContext(Guid.Empty)): the context
        // cannot own the event, so the command's self-declared TargetTenantId must take ownership.
        var tenantContext = new TenantContext();
        tenantContext.SetPlatformContext(Guid.Empty);
        var service = CreateService(writer, tenantContext);

        var result = await service.AppendAsync(ToAppendRequest(command));

        Assert.NotEqual(AuditAppendStatus.Rejected, result.Status);
        Assert.Equal(AuditAppendStatus.Queued, result.Status);
        Assert.NotNull(writer.Request);
        Assert.Equal(expectedTenantId, writer.Request!.TenantId);
    }

    [Theory]
    [MemberData(nameof(TenantScopedCommands))]
    public async Task GroupB_append_is_accepted_under_tenant_pinned_platform_context(IAuditableCommand command, Guid expectedTenantId)
    {
        var writer = new CapturingWriter();
        // Platform context already pinned to the target tenant — pre-existing behavior must be preserved.
        var tenantContext = new TenantContext();
        tenantContext.SetPlatformContext(expectedTenantId);
        var service = CreateService(writer, tenantContext);

        var result = await service.AppendAsync(ToAppendRequest(command));

        Assert.NotEqual(AuditAppendStatus.Rejected, result.Status);
        Assert.Equal(AuditAppendStatus.Queued, result.Status);
        Assert.NotNull(writer.Request);
        Assert.Equal(expectedTenantId, writer.Request!.TenantId);
    }

    // Mirrors AuditBehavior's metadata -> AuditAppendRequest projection so the tests exercise the same wiring.
    private static AuditAppendRequest ToAppendRequest(IAuditableCommand command)
    {
        var metadata = ((IAuditMetadataProvider)command).GetAuditMetadata();

        return new AuditAppendRequest
        {
            CorrelationId = metadata.CorrelationId ?? Guid.NewGuid(),
            RequestType = command.GetType().Name,
            ActorType = AuditActorType.PlatformAdministrator,
            Category = metadata.Category,
            EntityType = metadata.EntityType,
            EntityId = metadata.EntityId,
            Operation = metadata.Operation,
            Outcome = AuditOutcome.Succeeded,
            BeforeState = metadata.BeforeState,
            AfterState = metadata.AfterState,
            Metadata = metadata.Metadata ?? new Dictionary<string, object?>(),
            SourceService = "Diten.Platform",
            SourceModule = metadata.SourceModule,
            IsPlatformGlobal = metadata.IsPlatformGlobal,
            Sequence = metadata.Sequence,
            TargetTenantId = metadata.TargetTenantId,
            IsMetaAudit = metadata.IsMetaAudit
        };
    }

    private static AuditService CreateService(CapturingWriter writer, ITenantContext tenantContext)
    {
        return new AuditService(
            writer,
            new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
            new AuditIdempotencyKeyBuilder(),
            new AuditRecursionGuard(),
            tenantContext,
            new StubCurrentUserContext(),
            NullLogger<AuditService>.Instance);
    }

    private static CreateModuleCatalogItemRequest SampleCreateRequest()
    {
        return new CreateModuleCatalogItemRequest(
            "MOD-TEST", "Test Module", "Test Module", null, "Administration", "platform",
            "Active", "1.0", false, true, null);
    }

    private static UpdateModuleCatalogItemRequest SampleUpdateRequest()
    {
        return new UpdateModuleCatalogItemRequest(
            "MOD-TEST", "Test Module", "Test Module", null, "Administration", "platform",
            "Active", "1.0", false, true, null);
    }

    private sealed class CapturingWriter : IAuditOutboxWriter
    {
        public AuditOutboxWriteRequest? Request { get; private set; }

        public Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
        {
            Request = request;
            return Task.FromResult(true);
        }
    }

    private sealed class StubCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("b72ded0b-43c8-4e95-8158-bd56e391deaa");
        public string? Email => "admin@diten.com";
        public string? DisplayName => "Platform Admin";
        public string ActorName => "admin@diten.com";
        public bool IsAuthenticated => true;
    }
}
