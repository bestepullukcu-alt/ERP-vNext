using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Events;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class PpmProvisioningEntitlementPolicyTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Tenant_activation_real_controller_path_with_ppm_entitlement_creates_zero_grants()
    {
        var permission = new Permission("ppm", "projects", "read", "Read projects", null);
        var (sync, grants, _) = PpmEntitlementTestHarness.Create(TenantId, [permission]);
        var controller = Build(sync, new EntitlementClient([new("PPM", [permission.Key])]));

        var result = await controller.TenantActivated(Event(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(grants.Rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tenant_activation_empty_or_unreachable_entitlement_pull_is_fail_safe(bool throws)
    {
        var (sync, grants, _) = PpmEntitlementTestHarness.Create(TenantId, []);
        var controller = Build(sync, new EntitlementClient([], throws));

        var result = await controller.TenantActivated(Event(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(grants.Rows);
    }

    private static InternalEventsController Build(
        IEntitlementPermissionSyncService sync,
        ITenantEntitlementClient entitlementClient)
    {
        var controller = new InternalEventsController(
            new AllowInternalAuth(), new NoOpRoleProvisioning(), entitlementClient, sync, new FirstInbox(),
            null!, null!, null!, null!, null!, null!, null!,
            NullLogger<InternalEventsController>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Internal-Api-Key"] = "test";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static TenantActivatedIntegrationEvent Event() => new(
        Guid.NewGuid(), TenantId, "tenant.activated", 1, Guid.NewGuid(), Guid.NewGuid(),
        DateTimeOffset.UtcNow, "test");

    private sealed class AllowInternalAuth : IInternalEventAuthService
    {
        public bool IsAuthorized(string? apiKeyHeaderValue) => true;
    }

    private sealed class NoOpRoleProvisioning : IRoleProvisioningService
    {
        public Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FirstInbox : IIntegrationEventInboxRepository
    {
        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class EntitlementClient(
        IReadOnlyList<EntitledModulePermissionKeys> modules,
        bool throws = false) : ITenantEntitlementClient
    {
        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(modules.Select(x => x.ModuleCode).ToArray());

        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(
            Guid tenantId, CancellationToken ct)
            => throws
                ? Task.FromException<IReadOnlyList<EntitledModulePermissionKeys>>(new HttpRequestException("unavailable"))
                : Task.FromResult(modules);
    }
}
