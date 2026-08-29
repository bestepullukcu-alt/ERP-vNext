using System.Xml.Linq;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Infrastructure;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsB10BoundaryArchitectureTests
{
    private static readonly string[] ForbiddenForeignReferences =
    [
        "Diten.Platform",
        "Diten.PvgService",
        "Diten.EnterpriseStrategyService",
        "Diten.PpmService",
        "Diten.AuthService",
        "frontend/Diten.Web",
        "gateway/Diten.ApiGateway",
        "WorkCenter"
    ];

    private static readonly string[] ForbiddenAuditAndEventingTokens =
    [
        "audit_outbox",
        "audit_events",
        "/api/internal/audit/append",
        "IEventBus",
        "PublishAsync",
        "MassTransit",
        "RabbitMQ"
    ];

    [Fact]
    public void Five_layer_project_graph_is_exact_and_has_no_foreign_surface_reference()
    {
        var root = FindRoot();
        var sourceRoot = Path.Combine(root, "services/Diten.ManagementGovernanceService/src");
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Diten.ManagementGovernanceService.Domain"] = [],
            ["Diten.ManagementGovernanceService.Application"] = ["Diten.ManagementGovernanceService.Domain"],
            ["Diten.ManagementGovernanceService.Persistence"] =
            [
                "Diten.ManagementGovernanceService.Application",
                "Diten.ManagementGovernanceService.Domain"
            ],
            ["Diten.ManagementGovernanceService.Infrastructure"] =
            [
                "Diten.ManagementGovernanceService.Application",
                "Diten.ManagementGovernanceService.Persistence"
            ],
            ["Diten.ManagementGovernanceService.Api"] =
            [
                "Diten.ManagementGovernanceService.Application",
                "Diten.ManagementGovernanceService.Infrastructure",
                "Diten.ManagementGovernanceService.Persistence"
            ]
        };

        var projects = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), projects.Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal));

        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var references = XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")?.Value))
                .Where(value => value is not null)
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected[name].Order(StringComparer.Ordinal), references);
            Assert.All(ForbiddenForeignReferences, forbidden =>
                Assert.DoesNotContain(forbidden, File.ReadAllText(project), StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Dws_has_no_direct_platform_audit_collection_shared_key_endpoint_or_live_eventing_access()
    {
        var files = DwsProductionFiles();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.All(ForbiddenAuditAndEventingTokens, forbidden =>
                Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase));
            Assert.All(ForbiddenForeignReferences, forbidden =>
                Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase));
        }

        Assert.All(DwsMongoContext.CollectionAliases.Values, collection =>
            Assert.StartsWith("mg_dws_", collection, StringComparison.Ordinal));
        Assert.DoesNotContain(DwsMongoContext.CollectionAliases.Values, collection =>
            collection.Equals("audit_outbox", StringComparison.OrdinalIgnoreCase)
            || collection.Equals("audit_events", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_composition_has_no_local_executor_live_provider_or_runtime_activation()
    {
        var services = new ServiceCollection();
        services.AddDwsInfrastructure();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDwsLocalActionExecutor));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMod0117ContextValidationAdapter));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IFu16DwsAuthorizationAdapter));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDwsAuditSimulator));
        Assert.False(DwsInfrastructureBoundary.RuntimeActivationEnabled);
    }

    [Fact]
    public async Task Local_host_remains_default_off_and_loopback_only()
    {
        var execution = Diten.ManagementGovernanceService.Api.Program.Main([]);
        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(execution.IsCompletedSuccessfully);

        var app = Diten.ManagementGovernanceService.Api.Program.BuildLocalTestApp(
            "mongodb://127.0.0.1:65535",
            "b10_configuration_only");
        try
        {
            Assert.Equal("http://127.0.0.1:5017", app.Configuration["urls"]);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public void B11A_dispatch_and_storage_are_explicit_local_smoke_not_functional_Dws_truth()
    {
        var root = FindRoot();
        var adapterPath = Path.Combine(
            root,
            "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Infrastructure/Modules/Dws/DwsLocalTestAdapters.cs");
        var source = File.ReadAllText(adapterPath);

        Assert.Contains("return new(request.Operation, \"validated\"", source, StringComparison.Ordinal);
        Assert.Contains("new BsonDocument(\"Value\", request.Operation)", source, StringComparison.Ordinal);
        Assert.Equal(["CorrelationId", "Operation", "Outcome"],
            typeof(DwsLocalResult).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));

        var implementations = typeof(DwsMongoLocalActionExecutor).Assembly.GetTypes()
            .Where(type => typeof(IDwsLocalActionExecutor).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .ToArray();
        Assert.Equal([typeof(DwsMongoLocalActionExecutor)], implementations);
        Assert.Equal("DwsMongoLocalActionExecutor", implementations.Single().Name);
    }

    private static IReadOnlyList<string> DwsProductionFiles()
    {
        var sourceRoot = Path.Combine(FindRoot(), "services/Diten.ManagementGovernanceService/src");
        return Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}Dws{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}Features{Path.DirectorySeparatorChar}Dws{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.EndsWith($"{Path.DirectorySeparatorChar}DwsStructuresController.cs", StringComparison.Ordinal)
                || path.EndsWith($"{Path.DirectorySeparatorChar}DependencyInjection.cs", StringComparison.Ordinal)
                || path.EndsWith($"{Path.DirectorySeparatorChar}Program.cs", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
