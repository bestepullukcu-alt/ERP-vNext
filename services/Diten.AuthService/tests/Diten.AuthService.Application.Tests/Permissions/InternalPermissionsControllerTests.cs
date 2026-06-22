using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Permissions;

// MODULE-CATALOG AUTOMATION Phase 1 — internal upsert endpoint: auth gate, format gate, idempotency.
public sealed class InternalPermissionsControllerTests
{
    private const string ApiKey = "internal-key";
    private const string Header = "X-Internal-Api-Key";

    [Fact]
    public async Task Missing_api_key_is_unauthorized_and_no_permission_written()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: false);

        var result = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", null, null), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Empty(repo.Items);
    }

    [Theory]
    [InlineData("goldenslim.records")]   // 2 segments
    [InlineData("not a key")]
    [InlineData("goldenslim.records.read_all")]
    public async Task Malformed_key_is_bad_request_and_no_permission_written(string key)
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        var result = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest(key, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task First_sync_creates_permission()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        var result = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "Read Golden Slim", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalPermissionsController.SyncPermissionResponse>(ok.Value);
        Assert.Equal("goldenslim.records.read", body.Key);
        Assert.Equal("created", body.Status);
        var permission = Assert.Single(repo.Items);
        Assert.Equal("goldenslim.records.read", permission.Key);
        Assert.Equal("Read Golden Slim", permission.DisplayName);
    }

    [Fact]
    public async Task Repeated_sync_of_same_key_is_idempotent_single_record()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "First", null), CancellationToken.None);
        var second = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("GOLDENSLIM.records.read", "Second", "desc"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(second);
        var body = Assert.IsType<InternalPermissionsController.SyncPermissionResponse>(ok.Value);
        Assert.Equal("updated", body.Status);
        // Same key (case-insensitive) → no duplicate row; display metadata refreshed.
        var permission = Assert.Single(repo.Items);
        Assert.Equal("goldenslim.records.read", permission.Key);
        Assert.Equal("Second", permission.DisplayName);
        Assert.Equal("desc", permission.Description);
    }

    [Fact]
    public async Task GetModules_without_api_key_is_unauthorized()
    {
        var controller = Build(new FakePermissionRepository(), authorized: false);

        var result = await controller.GetModules(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetModules_returns_distinct_modules_with_counts_and_preserves_case()
    {
        var repo = new FakePermissionRepository();
        repo.Items.Add(new Permission("goldenslim", "records", "read", "x", null));
        repo.Items.Add(new Permission("goldenslim", "records", "create", "x", null));
        repo.Items.Add(new Permission("mdm", "legal-entities", "read", "x", null));
        var controller = Build(repo, authorized: true);

        var result = await controller.GetModules(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var modules = Assert.IsAssignableFrom<IEnumerable<InternalPermissionsController.PermissionModuleSummary>>(ok.Value).ToList();
        Assert.Equal(2, modules.Count);
        Assert.Contains(modules, m => m.Module == "goldenslim" && m.PermissionCount == 2);
        Assert.Contains(modules, m => m.Module == "mdm" && m.PermissionCount == 1);
    }

    private static InternalPermissionsController Build(FakePermissionRepository repo, bool authorized)
    {
        var controller = new InternalPermissionsController(
            new FakeInternalEventAuthService(authorized ? ApiKey : null),
            repo,
            NullLogger<InternalPermissionsController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[Header] = ApiKey;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class FakeInternalEventAuthService(string? expectedKey) : IInternalEventAuthService
    {
        public bool IsAuthorized(string? apiKeyHeaderValue) =>
            expectedKey is not null && string.Equals(expectedKey, apiKeyHeaderValue, StringComparison.Ordinal);
    }

    private sealed class FakePermissionRepository : IPermissionRepository
    {
        public List<Permission> Items { get; } = [];

        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Key == key.ToLowerInvariant()));

        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct)
        {
            Items.Add(permission);
            return Task.FromResult(permission);
        }

        public Task UpdateAsync(Permission permission, CancellationToken ct)
        {
            var index = Items.FindIndex(p => p.Id == permission.Id);
            if (index >= 0)
            {
                Items[index] = permission;
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => Task.FromResult<IEnumerable<Permission>>(Items);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
