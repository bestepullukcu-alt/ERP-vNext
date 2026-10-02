using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests.Workflow;

// The processor's guards are constructor dependencies. These tests resolve it through the registration the host
// itself calls (Program.cs → AddGlobalProductIdentityWorkflowProcessor + AddInfrastructure), not a hand-built one.
public sealed class GlobalProductIdentityWorkflowProcessorRegistrationTests
{
    [Fact]
    public async Task Host_registration_gives_the_processor_the_production_readiness_and_it_guards_the_submit()
    {
        await using var provider = HostServices().BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<ProductIdentityWorkflowTransportReadiness>(
            scope.ServiceProvider.GetRequiredService<IProductIdentityWorkflowTransportReadiness>());
        var settings = scope.ServiceProvider.GetRequiredService<ProductIdentityWorkflowServiceFaultSettings>();
        Assert.Equal(TimeSpan.FromMinutes(15), settings.MaxRetryDelay);
        Assert.Equal(5, settings.ErrorAfterAttempts);

        // Nothing is configured in this host: the container-built processor refuses before it reads or writes.
        var result = await scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>()
            .StartInteractiveAsync(Guid.NewGuid(), Guid.NewGuid(), 0, Guid.NewGuid(), Guid.NewGuid(), "maker-token",
                TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID", result.ErrorCode);
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<UntouchedStores>().OperationReads);
        Assert.Equal(0, scope.ServiceProvider.GetRequiredService<UntouchedStores>().Writes);
    }

    [Fact]
    public async Task Without_the_readiness_registration_the_processor_cannot_be_resolved()
    {
        var services = HostServices();
        var readiness = Assert.Single(services,
            descriptor => descriptor.ServiceType == typeof(IProductIdentityWorkflowTransportReadiness));
        Assert.True(services.Remove(readiness));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var error = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>());

        Assert.Contains(nameof(IProductIdentityWorkflowTransportReadiness), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("120", "2", 120, 2)]
    [InlineData("86400", "1000", 86400, 1000)]
    public async Task Service_fault_thresholds_are_read_from_configuration(
        string ceiling, string threshold, int expectedCeilingSeconds, int expectedThreshold)
    {
        await using var provider = HostServices(new()
        {
            ["ProductIdentityWorkflowWorker:ServiceFaultMaxRetryDelaySeconds"] = ceiling,
            ["ProductIdentityWorkflowWorker:ServiceFaultErrorAfterAttempts"] = threshold
        }).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var settings = scope.ServiceProvider.GetRequiredService<ProductIdentityWorkflowServiceFaultSettings>();

        Assert.Equal(TimeSpan.FromSeconds(expectedCeilingSeconds), settings.MaxRetryDelay);
        Assert.Equal(expectedThreshold, settings.ErrorAfterAttempts);
    }

    [Theory]
    [InlineData("0", "5")]
    [InlineData("86401", "5")]
    [InlineData("900", "0")]
    [InlineData("900", "1001")]
    public async Task Service_fault_thresholds_out_of_range_fail_closed(string ceiling, string threshold)
    {
        await using var provider = HostServices(new()
        {
            ["ProductIdentityWorkflowWorker:ServiceFaultMaxRetryDelaySeconds"] = ceiling,
            ["ProductIdentityWorkflowWorker:ServiceFaultErrorAfterAttempts"] = threshold
        }).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var error = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>());

        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", error.Message);
    }

    [Fact]
    public void Program_registers_the_processor_only_through_the_tested_seam()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var program = File.ReadAllText(Path.Combine(root!.FullName, "services", "Diten.MdmService", "src",
            "Diten.MdmService.Api", "Program.cs"));

        Assert.Equal(1, Count(program, "builder.Services.AddGlobalProductIdentityWorkflowProcessor();"));
        Assert.Equal(1, Count(program, "AddScoped<GlobalProductIdentityWorkflowProcessor>()")); // inside the seam
        Assert.Equal(0, Count(program, "builder.Services.AddScoped<GlobalProductIdentityWorkflowProcessor>()"));
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    private static ServiceCollection HostServices(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        services.Configure<ProductIdentityWorkflowWorkerOptions>(
            configuration.GetSection(ProductIdentityWorkflowWorkerOptions.SectionName));
        services.AddGlobalProductIdentityWorkflowProcessor();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new ProductIdentityWorkflowStartConfiguration(
            Guid.NewGuid(), null, [Guid.NewGuid()], "IDENTITY_APPROVAL", true, true, null));
        services.AddScoped<ProductIdentityWorkflowStartRequestFactory>();
        services.AddSingleton<UntouchedStores>();
        services.AddScoped(provider => provider.GetRequiredService<UntouchedStores>().Operations);
        services.AddScoped(provider => provider.GetRequiredService<UntouchedStores>().Products);
        return services;
    }

    // Stores that count what reaches them. A read of the operation is allowed (it is how a repeat is recognised);
    // any other member throws, so a write or a product read fails the test loudly.
    private sealed class UntouchedStores
    {
        public UntouchedStores()
        {
            Operations = System.Reflection.DispatchProxy.Create<IGlobalProductIdentityWorkflowOperationRepository, Recorder>();
            ((Recorder)(object)Operations).Owner = this;
            Products = System.Reflection.DispatchProxy.Create<IGlobalProductRepository, Recorder>();
            ((Recorder)(object)Products).Owner = this;
        }

        public IGlobalProductIdentityWorkflowOperationRepository Operations { get; }
        public IGlobalProductRepository Products { get; }
        public int OperationReads { get; set; }
        public int Writes { get; set; }
    }

    public class Recorder : System.Reflection.DispatchProxy
    {
        internal object? Owner { get; set; }

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            var owner = (UntouchedStores)Owner!;
            if (targetMethod!.Name == nameof(IGlobalProductIdentityWorkflowOperationRepository.GetByOperationIdAsync))
            {
                owner.OperationReads++;
                return Task.FromResult<Diten.MdmService.Domain.Entities.GlobalProductIdentityWorkflowOperation?>(null);
            }

            owner.Writes++;
            throw new InvalidOperationException($"{targetMethod.Name} must not be reached.");
        }
    }
}
