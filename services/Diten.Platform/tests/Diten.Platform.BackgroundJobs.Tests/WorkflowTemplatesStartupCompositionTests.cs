using Diten.Platform.API.Configuration;
using Diten.Platform.Application;
using Diten.Platform.Application.Features.Workflow.Operations;
using Diten.Platform.Infrastructure;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

/// <summary>
/// WP-PLT-WORKFLOW-TEMPLATES-01 (g) — a NORMAL Platform start never installs approval templates: the one-shot mode is chosen
/// only by its exact argument, Program.cs dispatches it before the web host is built (and returns), and no hosted service of
/// the Platform assemblies touches the command or its catalog.
/// </summary>
public sealed class WorkflowTemplatesStartupCompositionTests
{
    [Fact]
    public void Only_the_command_argument_selects_the_mode()
    {
        Assert.False(WorkflowTemplatesOperationalMode.IsRequested([]));
        Assert.False(WorkflowTemplatesOperationalMode.IsRequested(["--urls", "http://127.0.0.1:5057"]));
        Assert.False(WorkflowTemplatesOperationalMode.IsRequested([ApiStartupExecutionMode.ServeWithoutStartupMaintenanceArgument]));
        Assert.True(WorkflowTemplatesOperationalMode.IsRequested([EnsureWorkflowTemplatesCommandLine.Argument, "--tenant", Guid.NewGuid().ToString()]));
    }

    // v1.1 (5) — EXACT match only (ordinal): a case variant, a prefix or a "=value" form is not this command.
    [Theory]
    [InlineData("--ENSURE-WORKFLOW-TEMPLATES")]
    [InlineData("--Ensure-Workflow-Templates")]
    [InlineData("--ensure-workflow-templates=true")]
    [InlineData("--ensure-workflow-templates-now")]
    [InlineData("--ensure-workflow-template")]
    public void A_near_miss_argument_does_not_select_the_mode(string argument)
    {
        Assert.False(WorkflowTemplatesOperationalMode.IsRequested([argument, "--tenant", Guid.NewGuid().ToString(), "--apply"]));
    }

    /// <summary>
    /// v1.1 (4) — THE NORMAL START, measured (the PlatformApiStartupExecutionModeTests pattern): the host's own composition
    /// (AddApplication + AddInfrastructure WITH its start-up maintenance — migrations, schema, every seed) over an empty
    /// database of the test process's own mongod writes no workflow definition and no version, and registers no hosted
    /// service that could install one later.
    /// </summary>
    [Fact]
    public async Task The_normal_start_composition_writes_no_workflow_definition_and_registers_no_template_installer()
    {
        const string databaseName = "diten_platform_itest_workflow_templates_normal_start";
        var connection = await PlatformTestMongo.ConnectionStringAsync();
        var client = new MongoClient(connection);
        await client.DropDatabaseAsync(databaseName);
        var database = client.GetDatabase(databaseName);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = connection,
                ["MongoDbSettings:DatabaseName"] = databaseName,
                ["JwtSettings:Secret"] = "workflow-templates-composition-test-only-jwt-secret-0123456789",
                ["JwtSettings:Issuer"] = "diten-platform-workflow-templates-tests",
                ["JwtSettings:Audience"] = "diten-platform-workflow-templates-tests",
                ["AuthService:BaseUrl"] = "http://127.0.0.1:1",
                ["AuthService:InternalApiKey"] = "workflow-templates-composition-test-only-internal-api-key",
                ["ModuleRegistrationCredentials:Mdm:Identifier"] = "ditenmdmservice",
                ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] = "workflow-templates-composition-test-only-module-secret",
                ["BackgroundJobs:Enabled"] = "false",
                ["BackgroundJobs:DashboardEnabled"] = "false",
                ["Eventing:Transport"] = "InMemory",
                ["Eventing:WorkerEnabled"] = "false",
                ["Smtp:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();

        services.AddInfrastructure(configuration, new HostEnvironment("Staging"), runStartupMaintenance: true);

        Assert.True(await database.GetCollection<BsonDocument>(PlatformCollections.Tenants)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) > 0, "the start-up maintenance did not run"); // non-vacuity
        Assert.Equal(0, await database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplates)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplateVersions)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var hostedTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType?.FullName ?? descriptor.ImplementationFactory?.Method.ReturnType.FullName ?? string.Empty)
            .ToList();
        Assert.NotEmpty(hostedTypes); // non-vacuity: the normal composition has hosted services
        Assert.DoesNotContain(hostedTypes, name => name.Contains("WorkflowTemplate", StringComparison.Ordinal));
    }

    private sealed class HostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.Platform.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Program_dispatches_the_mode_before_the_web_host_and_only_when_requested()
    {
        var program = File.ReadAllText(RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));
        var requested = program.IndexOf("if (WorkflowTemplatesOperationalMode.IsRequested(args))", StringComparison.Ordinal);
        var run = program.IndexOf("await WorkflowTemplatesOperationalMode.RunAsync(args)", StringComparison.Ordinal);
        var builder = program.IndexOf("WebApplication.CreateBuilder", StringComparison.Ordinal);

        Assert.True(requested >= 0 && run > requested && builder > run, "the mode must be dispatched before the web host is built");
        Assert.Equal(1, Occurrences(program, "WorkflowTemplatesOperationalMode.RunAsync"));
        Assert.Contains("return;", program[run..builder], StringComparison.Ordinal); // the process ends after the command
    }

    [Fact]
    public void No_hosted_service_of_the_platform_installs_templates()
    {
        var assemblies = new[]
        {
            typeof(WorkflowTemplatesOperationalMode).Assembly,
            typeof(EnsureWorkflowTemplatesCommandLine).Assembly,
            typeof(Diten.Platform.Infrastructure.DependencyInjection).Assembly
        };
        var hosted = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IHostedService).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();
        Assert.NotEmpty(hosted); // non-vacuity: the scan sees the hosted services

        Assert.DoesNotContain(hosted, type => type.Name.Contains("WorkflowTemplate", StringComparison.Ordinal));
        var platformSources = Directory.EnumerateFiles(RepoFile("services", "Diten.Platform", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Where(path => File.ReadAllText(path).Contains("EnsureWorkflowTemplatesCommandLine.RunAsync", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .ToList();
        Assert.Equal(["WorkflowTemplatesOperationalMode.cs"], platformSources); // the one caller is the operator mode
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string RepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return Path.Combine([current?.FullName ?? throw new InvalidOperationException("Checkout required"), .. segments]);
    }
}
