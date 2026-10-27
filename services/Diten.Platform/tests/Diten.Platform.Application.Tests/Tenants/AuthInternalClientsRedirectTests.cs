using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

/// <summary>
/// BL-454 FIX3 — every Platform call that carries a credential to AuthService, made for real against a loopback server
/// that answers <c>302</c> to a second path on the same host. The internal key travels in a custom header, which
/// HttpClient keeps across a redirect; the trap path must therefore never be reached. The clients are built from the
/// production registration (<see cref="InternalHttpClients.AddAuthInternalHttpClients"/>, the call AddInfrastructure
/// makes); the container test in Diten.Platform.BackgroundJobs.Tests proves AddInfrastructure makes it.
/// </summary>
public sealed class AuthInternalClientsRedirectTests
{
    public static TheoryData<string> Callers() =>
    [
        "AuthPermissionModulesClient", "AuthServiceTenantActivationNotifier", "AuthTaskNotificationRecipientClient",
        "AuthTenantUserCountClient", "AuthUserDisplayNameClient", "CatalogPermissionSyncService.Sync",
        "CatalogPermissionSyncService.Remove", "PlatformAdministratorProvisioningService.Provision",
        "PlatformAdministratorProvisioningService.Sync", "AdminUserInvitationService", "IApprovalRoleDirectory",
        "IUserReferenceValidator"
    ];

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task A_redirect_answer_is_never_followed_with_the_credential(string caller)
    {
        await using var server = await RedirectingServer.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.Configure<AuthServiceOptions>(options =>
        {
            options.BaseUrl = server.Url;
            options.InternalApiKey = "fix3-test-only-internal-key";
            // BL-454 stage D FIX1 — the invitation checks its link root before calling AuthService: a public https root.
            options.FrontendBaseUrl = "https://app.fix3.test";
        });
        services.AddSingleton<ITenantContext>(new FakeTenantContext(Guid.NewGuid()));
        services.AddSingleton<IMediator>(new NothingMediator());
        services.AddAuthInternalHttpClients();
        await using var provider = services.BuildServiceProvider();

        try
        {
            await Call(provider, caller);
        }
        catch (Exception)
        {
            // Several callers throw on a non-success answer; what matters here is where the request went.
        }

        Assert.True(server.First > 0, $"{caller} never reached the server: the test proves nothing.");
        Assert.Equal(0, server.Trapped);
    }

    private static async Task Call(IServiceProvider sp, string caller)
    {
        var ct = CancellationToken.None;
        T Make<T>() => ActivatorUtilities.CreateInstance<T>(sp);
        switch (caller)
        {
            case "AuthPermissionModulesClient": await Make<AuthPermissionModulesClient>().GetModulesAsync(ct); break;
            case "AuthServiceTenantActivationNotifier": await Make<AuthServiceTenantActivationNotifier>().NotifyActivatedAsync(Guid.NewGuid(), ct); break;
            case "AuthTaskNotificationRecipientClient": await Make<AuthTaskNotificationRecipientClient>().ResolveAsync([Guid.NewGuid()], ct); break;
            case "AuthTenantUserCountClient": await Make<AuthTenantUserCountClient>().GetCountsAsync(Guid.NewGuid(), ct); break;
            case "AuthUserDisplayNameClient": await Make<AuthUserDisplayNameClient>().ResolveAsync([Guid.NewGuid()], ct); break;
            case "CatalogPermissionSyncService.Sync":
                await Make<CatalogPermissionSyncService>().SyncPermissionAsync("platform.tasks.view", "View tasks", "tasks", "tenant", ct); break;
            case "CatalogPermissionSyncService.Remove": await Make<CatalogPermissionSyncService>().RemovePermissionAsync("platform.tasks.view", ct); break;
            case "PlatformAdministratorProvisioningService.Provision":
                await Make<PlatformAdministratorProvisioningService>().ProvisionAsync(
                    new PlatformAdministratorProvisioningRequest("ops@di10.test", "ops", "Ops", "platform_admin", ["PlatformAdmin"], true), ct); break;
            case "PlatformAdministratorProvisioningService.Sync":
                await Make<PlatformAdministratorProvisioningService>().SyncAsync(
                    new PlatformAdministratorProvisioningSyncRequest("ops@di10.test", "ops", "Ops", "platform_admin", ["PlatformAdmin"], "Active"), ct); break; // Merge WP-CT-LANE-BL521-MERGE-01 — BL-521 FIX4 item 5: the record's status travels
            case "AdminUserInvitationService":
                var tenant = new Tenant { Code = "DITEN", Slug = "diten", Name = "diten", DisplayName = "Diten", Domain = "diten.test" };
                var admin = new TenantAdminUser { Id = Guid.NewGuid(), Name = "Admin", Email = "admin@diten.test" };
                tenant.AdminUsers.Add(admin);
                await Make<AdminUserInvitationService>().InviteAsync(tenant, admin, AdminInvitationTrigger.Operator, ct); break;
            case "IApprovalRoleDirectory": await sp.GetRequiredService<IApprovalRoleDirectory>().ResolveAsync(["Approver"], ct); break;
            case "IUserReferenceValidator": await sp.GetRequiredService<IUserReferenceValidator>().ValidateAsync(Guid.NewGuid(), ct); break;
            default: throw new ArgumentOutOfRangeException(nameof(caller), caller, null);
        }
    }

    /// <summary>
    /// Answers every request with a 302 to <c>/trap</c> on the same host, and counts both. TEST-HIZ-01: a real Kestrel
    /// bound directly to 127.0.0.1:0, its port read after start. It used to be an HttpListener on a port found free
    /// and released first, and stopped with Stop() then Close(): on Linux/macOS Close() binds that port again to remove
    /// it, and when another socket had taken the port in between it threw "Address already in use" (CI, 2026-10-26);
    /// the same window existed between the probe and Start(). Binding port 0 and stopping once leave no window.
    /// </summary>
    internal sealed class RedirectingServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private int _first;
        private int _trapped;

        private RedirectingServer(WebApplication app) => _app = app;

        public string Url { get; private set; } = "";
        public int First => Volatile.Read(ref _first);
        public int Trapped => Volatile.Read(ref _trapped);

        public static async Task<RedirectingServer> StartAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var app = builder.Build();
            var server = new RedirectingServer(app);
            app.Run(server.AnswerAsync);
            try
            {
                await app.StartAsync();
                server.Url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
                    .FirstOrDefault()?.TrimEnd('/')
                    ?? throw new InvalidOperationException("The redirecting server did not report a bound address.");
                return server;
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }

        private async Task AnswerAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/trap"))
            {
                Interlocked.Increment(ref _trapped);
                context.Response.StatusCode = 200;
            }
            else
            {
                Interlocked.Increment(ref _first);
                context.Response.StatusCode = 302;
                context.Response.Headers.Location = Url + "/trap";
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{}");
        }

        public async ValueTask DisposeAsync()
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _app.StopAsync(stopping.Token);
            await _app.DisposeAsync();
        }
    }

    private sealed class NothingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => Task.FromResult(default(TResponse)!);
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
