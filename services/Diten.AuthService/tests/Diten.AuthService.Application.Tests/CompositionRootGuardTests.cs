using Diten.AuthService.Application.Tests.Persistence;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.AuthService.Application.Tests;

/// <summary>
/// Q366 — the composition-root guard. Three times on 2026-10-03 (Q217/Q236, Q271/Q272, Q362) a type was written,
/// unit-tested with a hand-built dependency graph, and never registered in the production container, so the feature
/// was dead while every test was green. This guard builds the REAL AuthService host (Program.cs, Development,
/// ValidateOnBuild + ValidateScopes) and fails when the production container cannot satisfy:
/// (1) a required constructor parameter of any controller — controllers are not registered services, so
///     ValidateOnBuild never sees them (Platform's InternalTenantLegalEntityScopeController, Q362);
/// (2) an OPTIONAL (= null) Diten.* constructor parameter of any registered type or controller — an optional
///     dependency that is never registered fails silently instead of loudly (Platform's MdmLegalEntityReferenceValidator, Q362).
/// AuthService builds its MongoClient and runs its seeder while Program.cs registers services, so the guard needs a real, disposable
/// mongod: set AUTH_COMPOSITION_GUARD_MONGO_URI. Unset, the guard FAILS — a guard that skips is not a guard.
/// Main sync FIX1 (CT decision B): unset, it now uses Auth's test-owned mongod (DITEN_TEST_MONGOD); with neither, it still FAILS by name.
/// No hosted service runs and no request is sent; the host is only built, once per process (the BSON
/// serializers it registers are process-global).
/// </summary>
[CollectionDefinition(CompositionRootGuardCollection.Name, DisableParallelization = true)]
public sealed class CompositionRootGuardCollection
{
    public const string Name = "Composition root guard";
}

[Collection(CompositionRootGuardCollection.Name)]
public sealed class CompositionRootGuardTests(CompositionRootGuardTests.Host host) : IClassFixture<CompositionRootGuardTests.Host>
{
    internal const string MongoVariable = "AUTH_COMPOSITION_GUARD_MONGO_URI";

    [Fact]
    public void EveryControllerConstructorParameter_IsResolvableFromTheProductionContainer()
    {
        var missing = host.Controllers.SelectMany(host.UnresolvableRequired).ToArray();
        Assert.True(missing.Length == 0, "Controllers the production container cannot construct:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryOptionalDitenDependency_IsRegisteredInTheProductionContainer()
    {
        var missing = host.ImplementationTypes.Concat(host.Controllers).Distinct().SelectMany(host.UnregisteredOptional).Distinct().ToArray();
        Assert.True(missing.Length == 0, "Optional Diten.* dependencies that silently resolve to null:\n" + string.Join("\n", missing));
    }

    public sealed class Host : IDisposable
    {
        private readonly Factory _factory;
        private readonly IServiceProviderIsService _isService;
        private readonly DisposableAuthMongoReplicaSet? _ownedMongo;

        public Host()
        {
            // Main sync FIX1 (CT decision B, the Platform decision C model): the guard's own variable first, as main wrote
            // it; otherwise Auth's test-owned mongod (DITEN_TEST_MONGOD, BL-570's resolver) — so CI's auth-app row runs it
            // with what it already sets; with neither, red by name. Never the shared Mongo: a given URI on the operational
            // band (or with no port) is refused.
            string mongo;
            var given = Environment.GetEnvironmentVariable(MongoVariable);
            if (given is not null)
            {
                mongo = ValidGivenUri(given);
            }
            else
            {
                RequireOwnedMongod(DisposableAuthMongoReplicaSet.ResolveExecutable());
                _ownedMongo = new DisposableAuthMongoReplicaSet();
                Task.Run(() => _ownedMongo.InitializeAsync()).GetAwaiter().GetResult();
                mongo = _ownedMongo.ConnectionString;
            }
            var settings = new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = mongo,
                ["MongoDbSettings:DatabaseName"] = "diten_auth_composition_guard",
                ["JwtSettings:Secret"] = "composition-guard-signing-key-0123456789abcdef",
                ["JwtSettings:Issuer"] = "composition-guard",
                ["JwtSettings:Audience"] = "composition-guard",
                ["Eventing:Transport"] = "InMemory"
            };
            // Program.cs reads configuration while it is still registering services, before WebApplicationFactory's
            // overrides apply; environment variables are read by CreateBuilder itself. Restored right after the build.
            var previous = settings.Keys.ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k.Replace(":", "__")));
            foreach (var (key, value) in settings) Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
            try
            {
                _factory = new Factory(settings);
                _isService = _factory.Services.GetRequiredService<IServiceProviderIsService>(); // builds the host
            }
            finally
            {
                foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
            }
            Controllers = typeof(Program).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t))
                .ToArray();
            // A typed HttpClient (AddHttpClient<TService, TImpl>) or any factory registration carries no
            // ImplementationType, so the descriptors alone miss it — MdmLegalEntityReferenceValidator is registered
            // exactly that way (sabotage S2, Q366). Every concrete Diten.* class that implements a registered
            // Diten.* service interface is therefore checked as well.
            var isService = _isService;
            ImplementationTypes = _factory.ImplementationTypes
                .Concat(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.StartsWith("Diten.", StringComparison.Ordinal) == true)
                    .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.OfType<Type>(); } })
                    .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                                && t.GetInterfaces().Any(i => i.Namespace?.StartsWith("Diten.", StringComparison.Ordinal) == true
                                                              && !i.IsGenericTypeDefinition && isService.IsService(i))))
                .Distinct()
                .ToArray();
        }

        public IReadOnlyList<Type> Controllers { get; }
        public IReadOnlyList<Type> ImplementationTypes { get; }

        public IEnumerable<string> UnresolvableRequired(Type type) =>
            Constructor(type)?.GetParameters()
                .Where(p => !p.HasDefaultValue && !_isService.IsService(p.ParameterType))
                .Select(p => $"{type.FullName}({p.ParameterType.FullName} {p.Name})") ?? Enumerable.Empty<string>();

        public IEnumerable<string> UnregisteredOptional(Type type) =>
            Constructor(type)?.GetParameters()
                .Where(p => p.HasDefaultValue && p.DefaultValue is null
                            && p.ParameterType.Namespace?.StartsWith("Diten.", StringComparison.Ordinal) == true
                            && !_isService.IsService(p.ParameterType))
                .Select(p => $"{type.FullName}({p.ParameterType.FullName}? {p.Name} = null)") ?? Enumerable.Empty<string>();

        // The constructor the container would use: the public one with the most parameters.
        private static ConstructorInfo? Constructor(Type type) =>
            type.IsGenericTypeDefinition ? null : type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

        public void Dispose()
        {
            _factory.Dispose();
            if (_ownedMongo is not null) Task.Run(() => _ownedMongo.DisposeAsync()).GetAwaiter().GetResult();
        }

        /// <summary>No given URI and no owned mongod: red BY NAME — never a skip, never a fallback to the shared Mongo.</summary>
        internal static void RequireOwnedMongod(string? executable)
        {
            if (executable is null)
                throw new InvalidOperationException(
                    $"{DisposableAuthMongoReplicaSet.RequiredButUnavailable}: neither {MongoVariable} nor {OwnedMongo.Variable} names a mongod; this guard must not be skipped.");
        }

        /// <summary>Main's rule (set and not blank) plus the shared-Mongo refusal: not the operational band, not portless.</summary>
        internal static string ValidGivenUri(string given)
        {
            if (string.IsNullOrWhiteSpace(given))
                throw new InvalidOperationException($"{MongoVariable} is not set. Point it at a disposable mongod (never the shared one); this guard must not be skipped.");
            if (new MongoDB.Driver.MongoUrl(given).Servers.Any(server => server.Port is >= 27017 and <= 27021))
                throw new InvalidOperationException($"{MongoVariable} names the shared Mongo's port band; point it at a disposable mongod.");
            return given;
        }
    }

    public sealed class Factory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
    {
        public List<Type> ImplementationTypes { get; } = [];

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseDefaultServiceProvider(o => { o.ValidateOnBuild = true; o.ValidateScopes = true; });
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(settings));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                ImplementationTypes.AddRange(services
                    .Where(d => !d.IsKeyedService && d.ImplementationType is { } t && t.Namespace?.StartsWith("Diten.", StringComparison.Ordinal) == true)
                    .Select(d => d.ImplementationType!));
                services.RemoveAll<IHostedService>();
            });
        }
    }
}
