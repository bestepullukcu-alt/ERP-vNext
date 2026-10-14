using Diten.AuthService.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Application.Tests.Email;

/// <summary>
/// BL-454 fix round 2 — every AuthService client that carries the internal API key never follows a redirect, measured on
/// the PRODUCTION registration: <c>AddInfrastructure</c> over the service's own appsettings, exactly as Program.cs calls
/// it. Nothing is started and nothing connects; the container is only asked how each client's transport is built.
/// A redirect would hand <c>X-Internal-Api-Key</c> to whatever host the answer named.
/// </summary>
public sealed class InternalClientsRegistrationTests
{
    /// <summary>The typed clients' names as AddHttpClient&lt;TClient, TImpl&gt; registers them.</summary>
    public static TheoryData<string> KeyCarryingClients => new()
    {
        "ITenantLoginSettingsClient",
        "IPlatformAdministratorStatusClient",
        "ITenantEntitlementClient",
        "ITenantAdminActivationClient",
        "IPlatformAuditForwarder",
        "IUserQuotaClient",
        "ITenantEmailIdentityClient"
    };

    [Theory]
    [MemberData(nameof(KeyCarryingClients))]
    public async Task A_client_that_carries_the_internal_key_is_built_without_redirects(string clientName)
    {
        await using var provider = Compose().BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        HttpMessageHandler handler = factory.CreateHandler(clientName);
        while (handler is DelegatingHandler delegating && delegating.InnerHandler is not null)
        {
            handler = delegating.InnerHandler;
        }

        var primary = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(primary.AllowAutoRedirect, $"{clientName} follows redirects.");
    }

    private static IServiceCollection Compose()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(ApiSettingsPath("appsettings.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Local-only literals: nothing is signed, authenticated or connected with them, because nothing starts.
                ["JwtSettings:Secret"] = "registration-test-only-jwt-signing-secret-0123456789",
                ["MongoDbSettings:ConnectionString"] = "mongodb://127.0.0.1:1",
                ["MongoDbSettings:DatabaseName"] = "diten_auth_registration_test",
                ["InternalEventAuth:ApiKey"] = "registration-test-only-internal-event-key",
                ["PlatformService:InternalApiKey"] = "registration-test-only-platform-key",
                ["PlatformService:BaseUrl"] = "http://127.0.0.1:1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration, new RegistrationHostEnvironment());
        return services;
    }

    private static string ApiSettingsPath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return Path.Combine(current!.FullName, "services", "Diten.AuthService", "src", "Diten.AuthService.Api", fileName);
    }

    private sealed class RegistrationHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Diten.AuthService.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
