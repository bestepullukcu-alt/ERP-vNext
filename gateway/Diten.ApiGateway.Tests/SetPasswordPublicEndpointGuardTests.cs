using Diten.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.ApiGateway.Tests;

/*
 * THE GUARD — ANONYMOUS INVITATION REDEMPTION SURVIVES THE GATEWAY (set-password whitelist sync).
 *
 * WHAT WAS WRONG. Invitation redemption ("Set your password") is deliberately ANONYMOUS: the invited user has
 * not signed in yet, so the request carries no JWT and no X-Tenant-Id — the user (and their tenant) are resolved
 * downstream from the token hash in the body. AuthService's own TenantResolutionMiddleware knew this and listed
 * `/api/users/set-password` in IsPublicAuthPath. The GATEWAY's TenantResolutionMiddleware did NOT list it in
 * IsPublicEndpoint, so the request never reached AuthService: it fell into the generic tenant branch and was
 * refused 400 "Missing Tenant". The two whitelists had drifted apart.
 *
 * THE RULE. `/api/users/set-password` is public at the gateway exactly as it is at AuthService. A request to it
 * with no tenant signal MUST pass through (the handler runs), NOT be refused "Missing Tenant".
 *
 * ⚠ NOT A VACUITY CHECK. A middleware that refused everything would fail the control below (a NON-public tenant
 * path with no tenant signal, which must still be refused 400), so a green suite means the whitelist entry is
 * actually doing the work.
 */
public sealed class SetPasswordPublicEndpointGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string SetPasswordPath = "/api/users/set-password";
    private const string NeutralHost = "app.diten.com";

    /// <summary>
    /// THE REGRESSION. Anonymous set-password (no JWT, no X-Tenant-Id) must not be refused "Missing Tenant" —
    /// it must pass through so AuthService can resolve the user from the token hash.
    /// </summary>
    [Fact]
    public async Task Anonymous_set_password_is_not_refused_Missing_Tenant()
    {
        var run = await RunAsync(SetPasswordPath, host: NeutralHost);

        Assert.True(run.HandlerRan, "anonymous invitation redemption was refused at the gateway");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
    }

    /// <summary>
    /// THE CONTROL — a non-public tenant path with the SAME missing-tenant shape is still refused 400. Without
    /// this, a middleware that let everything through would pass the regression test above for the wrong reason.
    /// </summary>
    [Fact]
    public async Task Control_a_non_public_tenant_path_with_no_tenant_signal_is_still_refused()
    {
        var run = await RunAsync("/api/users", host: NeutralHost);

        Assert.False(run.HandlerRan, "a tenant endpoint with no tenant signal was let through");
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
    }

    private static async Task<Run> RunAsync(string path, string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Host = new HostString(host);
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            new StubEnvironment(Environments.Production));

        await middleware.InvokeAsync(context);

        return new Run(ran, context.Response.StatusCode);
    }

    private sealed record Run(bool HandlerRan, int StatusCode);

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.ApiGateway.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
