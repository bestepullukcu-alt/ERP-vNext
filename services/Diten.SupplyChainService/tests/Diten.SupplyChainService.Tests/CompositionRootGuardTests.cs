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
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Infrastructure.Common;

namespace Diten.SupplyChainService.Tests;

/// <summary>
/// Q366 — the composition-root guard. Three times on 2026-10-03 (Q217/Q236, Q271/Q272, Q362) a type was written,
/// unit-tested with a hand-built dependency graph, and never registered in the production container, so the feature
/// was dead while every test was green. This guard builds the REAL SupplyChain host (Program.cs, Development,
/// ValidateOnBuild + ValidateScopes) and fails when the production container cannot satisfy:
/// (1) a required constructor parameter of any controller — controllers are not registered services, so
///     ValidateOnBuild never sees them (Platform's InternalTenantLegalEntityScopeController, Q362);
/// (2) an OPTIONAL (= null) Diten.* constructor parameter of any registered type or controller — an optional
///     dependency that is never registered fails silently instead of loudly (Platform's MdmLegalEntityReferenceValidator, Q362).
/// SupplyChain reads its Mongo settings while Program.cs registers services, so the guard needs a real, disposable
/// mongod: set SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI. Unset, the guard FAILS — a guard that skips is not a guard.
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
    private const string MongoVariable = "SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI";

    // Known, deliberate gaps — each must STILL be a gap (asserted below), so an entry cannot outlive its reason.
    // Capacity (MOD-0192) is not composed in Program.cs on purpose (ledger Q273: CapacityRepository needs
    // IDemandFixtureReader, which has no production implementation). MapControllers() still maps its controller, so
    // its routes exist and cannot be served — measured by this guard on 2026-10-03 (Q366), recorded as a finding,
    // not fixed here. Remove the entry when Q273 composes Capacity; the second assertion then forces it.
    private static readonly IReadOnlyDictionary<string, string> KnownUncomposed = new Dictionary<string, string>
    {
        ["Diten.SupplyChainService.Api.Features.CapacityPlans.CapacityPlansController"] = "Q273 — Capacity deliberately not composed"
    };

    [Fact]
    public void EveryControllerConstructorParameter_IsResolvableFromTheProductionContainer()
    {
        var all = host.Controllers.SelectMany(host.UnresolvableRequired).ToArray();
        var missing = all.Where(m => !KnownUncomposed.Keys.Any(k => m.StartsWith(k + "(", StringComparison.Ordinal))).ToArray();
        Assert.True(missing.Length == 0, "Controllers the production container cannot construct:\n" + string.Join("\n", missing));
        var stale = KnownUncomposed.Keys.Where(k => !all.Any(m => m.StartsWith(k + "(", StringComparison.Ordinal))).ToArray();
        Assert.True(stale.Length == 0, "KnownUncomposed entries that are now constructible — remove them:\n" + string.Join("\n", stale));
    }

    [Fact]
    public void EveryOptionalDitenDependency_IsRegisteredInTheProductionContainer()
    {
        var missing = host.ImplementationTypes.Concat(host.Controllers).Distinct().SelectMany(host.UnregisteredOptional).Distinct().ToArray();
        Assert.True(missing.Length == 0, "Optional Diten.* dependencies that silently resolve to null:\n" + string.Join("\n", missing));
    }

    // R-2 (PR #134). The five *ContextMiddleware classes take ILegalEntityScopeValidator, and the integration
    // factories replace it with a stub (TestHostLegalEntities) because a test host has no MDM to ask. That
    // replacement is only safe while production still wires the real one, so the real one is named here: without
    // this fact the validator could be stubbed everywhere and nothing would notice — K23's shape, implemented but
    // never active. AddHttpClient<TService, TImpl> carries no ImplementationType, so the descriptor cannot be
    // inspected; the service is resolved and its runtime type asserted instead.
    [Fact]
    public void LegalEntityScopeValidator_IsTheRealMdmOne_InTheProductionContainer()
    {
        using var scope = host.Services.CreateScope();
        var validator = scope.ServiceProvider.GetService<ILegalEntityScopeValidator>();
        Assert.NotNull(validator);
        Assert.Equal(typeof(MdmLegalEntityScopeValidator), validator.GetType());
    }

    public sealed class Host : IDisposable
    {
        private readonly Factory _factory;
        private readonly IServiceProviderIsService _isService;

        public Host()
        {
            var mongo = Environment.GetEnvironmentVariable(MongoVariable);
            if (string.IsNullOrWhiteSpace(mongo))
                throw new InvalidOperationException($"{MongoVariable} is not set. Point it at a disposable mongod (never 27017); this guard must not be skipped.");
            var settings = new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = mongo,
                ["Mongo:DatabaseName"] = "diten_supplychain_composition_guard",
                ["JwtSettings:Secret"] = "composition-guard-signing-key-0123456789abcdef",
                ["JwtSettings:Issuer"] = "composition-guard",
                ["JwtSettings:Audience"] = "composition-guard"
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
        // The production provider itself, for facts that must name the concrete type the container hands over
        // rather than merely that something is registered.
        public IServiceProvider Services => _factory.Services;

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

        public void Dispose() => _factory.Dispose();
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
