using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Events;
using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class InternalEventsControllerTests
{
    [Fact]
    public async Task Unavailable_snapshot_does_not_claim_event()
    {
        var inbox = new InboxFake();
        var controller = Create(new EntitlementFake(TenantEntitlementReadResult.Unavailable()), inbox, new SyncFake());

        var result = await controller.TenantActivated(Event(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0, inbox.Claims);
    }

    [Fact]
    public async Task Authoritative_empty_snapshot_does_not_claim_event()
    {
        var inbox = new InboxFake();
        var controller = Create(new EntitlementFake(TenantEntitlementReadResult.Confirmed([])), inbox, new SyncFake());
        var result = await controller.TenantActivated(Event(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0, inbox.Claims);
    }

    [Fact]
    public async Task Completed_claim_is_noop_without_roles_sync_or_complete()
    {
        var sync = new SyncFake();
        var inbox = new InboxFake { Result = IntegrationEventClaimResult.Completed };
        var result = await Create(Authoritative(), inbox, sync).TenantActivated(Event(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, sync.Calls);
        Assert.Equal(0, inbox.Completes);
    }

    [Fact]
    public async Task Sync_failure_releases_claim_and_same_event_can_retry()
    {
        var inbox = new InboxFake();
        var sync = new SyncFake { Fail = true };
        var controller = Create(Authoritative(), inbox, sync);
        var integrationEvent = Event();

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TenantActivated(integrationEvent, CancellationToken.None));
        Assert.Equal(1, inbox.Releases);
        sync.Fail = false;

        var result = await controller.TenantActivated(integrationEvent, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, inbox.Claims);
        Assert.Equal(1, inbox.Completes);
    }

    [Fact]
    public async Task Recovery_contamination_releases_claim_without_completing_tenant_activation()
    {
        var inbox = new InboxFake();
        var sync = new SyncFake { FailureMessage = "PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION" };
        var controller = Create(Authoritative(), inbox, sync);
        var integrationEvent = Event();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TenantActivated(integrationEvent, CancellationToken.None));

        Assert.Equal("PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION", exception.Message);
        Assert.Equal(1, inbox.Releases);
        Assert.Equal(0, inbox.Completes);
    }

    [Fact]
    public async Task Successful_sync_completes_claim_only_after_roles_and_grants()
    {
        var order = new List<string>();
        var inbox = new InboxFake(order);
        var sync = new SyncFake(order);
        var controller = Create(Authoritative(), inbox, sync, new RolesFake(order));

        var result = await controller.TenantActivated(Event(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["claim", "roles", "sync", "complete"], order);
    }

    [Theory]
    [InlineData(IntegrationEventClaimResult.Busy)]
    [InlineData(IntegrationEventClaimResult.TenantMismatch)]
    [InlineData(IntegrationEventClaimResult.IdentityMismatch)]
    public async Task Non_claimable_identity_fails_before_roles_or_sync(IntegrationEventClaimResult result)
    {
        var sync = new SyncFake();
        var inbox = new InboxFake { Result = result };
        var response = await Create(Authoritative(), inbox, sync).TenantActivated(Event(), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(response);
        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task Release_failure_does_not_mask_original_sync_exception()
    {
        var inbox = new InboxFake { ReleaseFails = true };
        var sync = new SyncFake { Fail = true };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(Authoritative(), inbox, sync).TenantActivated(Event(), CancellationToken.None));
        Assert.Equal("sync failed", exception.Message);
    }

    [Fact]
    public async Task Release_failure_does_not_mask_original_cancellation()
    {
        var inbox = new InboxFake { ReleaseFails = true };
        var sync = new SyncFake { Cancel = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(Authoritative(), inbox, sync).TenantActivated(Event(), CancellationToken.None));
        Assert.Equal(1, inbox.Releases);
    }

    private static InternalEventsController Create(
        EntitlementFake entitlement,
        InboxFake inbox,
        SyncFake sync,
        RolesFake? roles = null)
    {
        var controller = new InternalEventsController(
            new AuthFake(), roles ?? new RolesFake(), entitlement, sync, inbox,
            null!, null!, null!, null!, null!, null!, null!,
            NullLogger<InternalEventsController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["X-Internal-Api-Key"] = "valid";
        return controller;
    }

    private static EntitlementFake Authoritative() => new(TenantEntitlementReadResult.Confirmed(
        [new EntitledModulePermissionKeys("product-item-sku-master", ["mdm.global-products.read"])]));

    private static TenantActivatedIntegrationEvent Event() => new(
        Guid.NewGuid(), Guid.NewGuid(), "tenant.activated", 1, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "test");

    private sealed class AuthFake : IInternalEventAuthService { public bool IsAuthorized(string? value) => value == "valid"; }
    private sealed class RolesFake(List<string>? order = null) : IRoleProvisioningService
    { public Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default) { order?.Add("roles"); return Task.CompletedTask; } }
    private sealed class EntitlementFake(TenantEntitlementReadResult result) : ITenantEntitlementClient
    {
        public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(result);
        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EntitledModulePermissionKeys>>(result.Modules);
    }
    private sealed class SyncFake(List<string>? order = null) : IEntitlementPermissionSyncService
    {
        public bool Fail { get; set; }
        public string? FailureMessage { get; set; }
        public bool Cancel { get; set; }
        public int Calls { get; private set; }
        public Task SyncTenantModulesWithKeysAsync(Guid tenantId, IReadOnlyCollection<EntitledModulePermissionKeys> modules, string actor, CancellationToken ct = default)
        { Calls++; order?.Add("sync"); return Cancel ? Task.FromCanceled(new CancellationToken(true)) : FailureMessage is not null ? Task.FromException(new InvalidOperationException(FailureMessage)) : Fail ? Task.FromException(new InvalidOperationException("sync failed")) : Task.CompletedTask; }
        public Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default) => Task.CompletedTask;
        public Task SyncTenantModulesAsync(Guid tenantId, IReadOnlyCollection<string> entitledModuleCodes, string actor, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrantModuleWithKeysAsync(Guid tenantId, string moduleCode, IReadOnlyCollection<string> permissionKeys, string actor, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class InboxFake(List<string>? order = null) : IIntegrationEventInboxRepository
    {
        public IntegrationEventClaimResult Result { get; set; } = IntegrationEventClaimResult.Claimed;
        public bool ReleaseFails { get; set; }
        public int Claims { get; private set; }
        public int Releases { get; private set; }
        public int Completes { get; private set; }
        public Task<IntegrationEventClaim> TryClaimAsync(Guid eventId, string eventName, Guid tenantId, TimeSpan leaseDuration, CancellationToken ct = default)
        { Claims++; order?.Add("claim"); return Task.FromResult(new IntegrationEventClaim(Result, Result == IntegrationEventClaimResult.Claimed ? Guid.NewGuid() : null)); }
        public Task CompleteClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default)
        { Completes++; order?.Add("complete"); return Task.CompletedTask; }
        public Task ReleaseClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default)
        { Releases++; return ReleaseFails ? Task.FromException(new InvalidOperationException("release failed")) : Task.CompletedTask; }
        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default) => Task.FromResult(true);
    }
}
