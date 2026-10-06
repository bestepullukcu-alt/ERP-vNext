using Diten.AuthService.Application.Common;
using Diten.AuthService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Tenancy;

/// <summary>
/// BL-529 — the platform password / provisioning doors resolve the platform tenant when the request names none, on the REAL
/// <see cref="TenantResolutionMiddleware"/>: exact paths only, a tenant the request does name is kept, and the list works in
/// Development too (where the dev bypass, which runs first, is switched off).
/// </summary>
public sealed class TenantResolutionPlatformPathsTests
{
    private static readonly Guid PlatformTenantId = TenantResolutionMiddleware.PlatformTenantId;

    [Theory]
    [InlineData("/api/platform-auth/platform-admins/provision")]
    [InlineData("/api/platform-auth/platform-admins/sync")]
    [InlineData("/api/platform-auth/forgot-password")]
    [InlineData("/api/platform-auth/reset-password")]
    [InlineData("/API/Platform-Auth/Reset-Password")]
    public async Task A_listed_door_without_a_tenant_resolves_the_platform_tenant(string path)
    {
        var (_, tenant, ran) = await RunAsync(path, header: null, Environments.Production);

        Assert.True(ran());
        Assert.Equal(PlatformTenantId, tenant.TenantId);
    }

    [Fact]
    public async Task A_listed_door_keeps_the_tenant_the_request_names()
    {
        var named = Guid.NewGuid();

        var (_, tenant, ran) = await RunAsync("/api/platform-auth/forgot-password", named, Environments.Production);

        Assert.True(ran());
        Assert.Equal(named, tenant.TenantId);
    }

    [Theory]
    [InlineData("/api/platform-auth/platform-admins/provision/x")]
    [InlineData("/api/platform-auth/reset-passwordx")]
    [InlineData("/api/platform-auth/change-password/forced")]
    [InlineData("/api/users")]
    public async Task Anything_else_without_a_tenant_is_still_refused(string path)
    {
        var (context, tenant, ran) = await RunAsync(path, header: null, Environments.Production);

        Assert.False(ran());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenant.IsResolved);
    }

    [Fact]
    public async Task In_Development_without_the_bypass_the_list_still_resolves()
    {
        var (_, tenant, ran) = await RunAsync("/api/platform-auth/reset-password", header: null, Environments.Development);

        Assert.True(ran());
        Assert.Equal(PlatformTenantId, tenant.TenantId);
    }

    [Fact]
    public async Task In_Development_the_bypass_still_runs_first()
    {
        var bypass = Guid.NewGuid();

        var (_, tenant, _) = await RunAsync("/api/platform-auth/reset-password", header: null, Environments.Development,
            new Dictionary<string, string?> { ["TenantResolution:DevBypassEnabled"] = "true", ["TenantResolution:DevBypassTenantId"] = bypass.ToString() });

        Assert.Equal(bypass, tenant.TenantId);
    }

    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> Ran)> RunAsync(
        string path, Guid? header, string environment, Dictionary<string, string?>? settings = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (header.HasValue)
        {
            context.Request.Headers["X-Tenant-Id"] = header.Value.ToString();
        }

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build(),
            new StubEnvironment(environment));

        var tenant = new TenantContext();
        await middleware.InvokeAsync(context, tenant);
        return (context, tenant, () => ran);
    }

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.AuthService.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
