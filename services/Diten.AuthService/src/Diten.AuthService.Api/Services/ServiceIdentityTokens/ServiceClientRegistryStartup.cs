using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Persistence.Configurations;
using MongoDB.Driver;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

/// <summary>
/// MOD-0033-FU02 §F.1 — wiring of <c>ServiceClientRegistry:Clients[]</c> into the NORMAL Auth startup, in every
/// environment.
///
/// <para>Two moments. <see cref="AddServiceClientRegistry"/> reads and validates the section while services are being
/// registered — before the persistence layer seeds anything — and throws on the first invalid entry, so a bad section
/// stops Auth before any write. The hosted service then applies the validated declarations before the server accepts
/// a request. An absent or empty section registers nothing to apply and touches no database.</para>
/// </summary>
public static class ServiceClientRegistryStartup
{
    private static readonly string[] EntryFields =
    [
        nameof(ServiceClientRegistryEntryInput.ClientCode),
        nameof(ServiceClientRegistryEntryInput.ServiceName),
        nameof(ServiceClientRegistryEntryInput.AllowedAudience),
        nameof(ServiceClientRegistryEntryInput.CredentialHash),
        nameof(ServiceClientRegistryEntryInput.CredentialVersion),
        nameof(ServiceClientRegistryEntryInput.Tenants)
    ];

    public static IServiceCollection AddServiceClientRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var declarations = ServiceClientRegistryValidator.Validate(Read(configuration), new ServiceClientCredentialVerifier());
        services.AddSingleton(new ServiceClientRegistryDeclarations(declarations));
        services.AddScoped<ServiceClientRegistryReconciler>();
        services.AddHostedService<ServiceClientRegistryHostedService>();
        return services;
    }

    /// <summary>
    /// Reads the section as written. Anything unexpected is flagged, not ignored: an unknown key, and a value where
    /// a section or a list belongs (a list given as a single value has no children and would read as "empty").
    /// Key names are matched the way configuration matches them — case-insensitively.
    /// </summary>
    public static IReadOnlyList<ServiceClientRegistryEntryInput> Read(IConfiguration configuration)
    {
        var section = configuration.GetSection(ServiceClientRegistryValidator.SectionName);
        if (!string.IsNullOrEmpty(section.Value))
            throw NotPartOfTheContract("must be a section");

        var entries = new List<ServiceClientRegistryEntryInput>();
        foreach (var child in section.GetChildren())
        {
            if (!string.Equals(child.Key, "Clients", StringComparison.OrdinalIgnoreCase))
                throw NotPartOfTheContract("has a field that is not part of the contract");
            if (!string.IsNullOrEmpty(child.Value))
                throw NotPartOfTheContract("Clients must be a list");

            foreach (var entry in child.GetChildren())
            {
                string? Field(string name) => entry.GetSection(name).Value;
                var tenants = entry.GetSection(nameof(ServiceClientRegistryEntryInput.Tenants));
                entries.Add(new ServiceClientRegistryEntryInput(
                    entry.Key,
                    Field(nameof(ServiceClientRegistryEntryInput.ClientCode)),
                    Field(nameof(ServiceClientRegistryEntryInput.ServiceName)),
                    Field(nameof(ServiceClientRegistryEntryInput.AllowedAudience)),
                    Field(nameof(ServiceClientRegistryEntryInput.CredentialHash)),
                    Field(nameof(ServiceClientRegistryEntryInput.CredentialVersion)),
                    tenants.GetChildren().Select(tenant => tenant.Value).ToArray(),
                    HasUnknownField: entry.GetChildren().Any(field => !EntryFields.Contains(field.Key, StringComparer.OrdinalIgnoreCase))
                        || tenants.GetChildren().Any(tenant => tenant.GetChildren().Any()),
                    TenantsIsNotAList: !string.IsNullOrEmpty(tenants.Value)));
            }
        }

        return entries;
    }

    private static ServiceClientRegistryConfigurationException NotPartOfTheContract(string problem) =>
        new($"Configuration error: {ServiceClientRegistryValidator.SectionName} {problem}.");
}

public sealed record ServiceClientRegistryDeclarations(IReadOnlyList<ServiceClientDeclaration> Clients);

public sealed class ServiceClientRegistryHostedService(
    ServiceClientRegistryDeclarations declarations,
    IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (declarations.Clients.Count == 0) return;

        await using var scope = scopes.CreateAsyncScope();
        try
        {
            // The unique indexes are what make two Auth instances starting together produce ONE identity and ONE
            // grant. The normal startup already builds them; this is the same idempotent call, made here so the
            // registry never runs without them (the persistence layer's own startup step swallows its failures).
            await MongoDbIndexConfigurations.EnsureServiceIdentityIndexesAsync(
                scope.ServiceProvider.GetRequiredService<IMongoDatabase>());
            await scope.ServiceProvider.GetRequiredService<ServiceClientRegistryReconciler>()
                .ApplyAsync(declarations.Clients, cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException
            or ServiceIdentityPersistenceUnavailableException)
        {
            // Declared clients that could not be confirmed must not be discovered later as token failures.
            throw new InvalidOperationException(
                "Startup error: the declared service clients could not be applied because persistence is unavailable.",
                exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
