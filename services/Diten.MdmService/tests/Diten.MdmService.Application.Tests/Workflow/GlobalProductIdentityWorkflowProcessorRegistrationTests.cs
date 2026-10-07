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

    // A wrong window keeps the host from STARTING (item 9). It is validated once, by the start-up validator the
    // host runs before it serves anything — never by a factory that would turn every request into a 500.
    [Theory]
    [InlineData("0", "5", "30")]
    [InlineData("86401", "5", "30")]
    [InlineData("900", "0", "30")]
    [InlineData("900", "1001", "30")]
    [InlineData("20", "5", "30")] // a ceiling below the ordinary retry delay is not a back-off
    public async Task Service_fault_window_out_of_range_stops_the_host_at_start(
        string ceiling, string threshold, string retryDelay)
    {
        await using var provider = HostServices(new()
        {
            ["ProductIdentityWorkflowWorker:ServiceFaultMaxRetryDelaySeconds"] = ceiling,
            ["ProductIdentityWorkflowWorker:ServiceFaultErrorAfterAttempts"] = threshold,
            ["ProductIdentityWorkflowWorker:RetryDelaySeconds"] = retryDelay
        }).BuildServiceProvider();

        var error = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate());

        Assert.Contains("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", error.Failures);
    }

    // The lease and the retry delay too: submit and withdraw read them, so a wrong value used to turn every request
    // into a 500 — and it stops the host whether or not the worker is switched on.
    [Theory]
    [InlineData("LeaseSeconds", "9")]
    [InlineData("LeaseSeconds", "901")]
    [InlineData("RetryDelaySeconds", "0")]
    [InlineData("RetryDelaySeconds", "3601")]
    public async Task A_lease_or_retry_delay_out_of_range_stops_the_host_at_start_with_the_worker_off(
        string setting, string value)
    {
        await using var provider = HostServices(new()
        {
            ["ProductIdentityWorkflowWorker:Enabled"] = "false",
            ["ProductIdentityWorkflowWorker:ServiceFaultMaxRetryDelaySeconds"] = "86400",
            [$"ProductIdentityWorkflowWorker:{setting}"] = value
        }).BuildServiceProvider();

        var error = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate());

        Assert.Contains("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", error.Failures);
    }

    // BL-552 flow 3 FIX2 item 3: the settle window and the record-answer limit stop the host too — with the worker OFF
    // (the usual case), the start-up validator is the only guard: the interactive submit reads both.
    [Theory]
    [InlineData("StartSettleSeconds", "0")]
    [InlineData("StartSettleSeconds", "-1")]
    [InlineData("StartSettleSeconds", "1501")]           // flow 3 FIX3 EK: Platform's sweep age (25 min cap)
    [InlineData("QuarantineAfterFailedAttempts", "-5")]
    [InlineData("QuarantineAfterFailedAttempts", "-1")]  // flow 3 FIX3 item 8 (CT G5): the lower edge itself
    [InlineData("StuckGaugeIntervalSeconds", "14")]        // flow 3 FIX3 item 1: the gauge interval's bounds
    [InlineData("StuckGaugeIntervalSeconds", "3601")]
    [InlineData("QuarantineAfterFailedAttempts", "1001")]
    public async Task A_settle_window_or_record_answer_limit_out_of_range_stops_the_host_at_start_with_the_worker_off(
        string setting, string value)
    {
        await using var provider = HostServices(new()
        {
            ["ProductIdentityWorkflowWorker:Enabled"] = "false",
            [$"ProductIdentityWorkflowWorker:{setting}"] = value
        }).BuildServiceProvider();

        var error = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate());

        Assert.Contains("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", error.Failures);
    }

    [Fact]
    public async Task A_valid_window_passes_the_start_up_validation()
    {
        await using var provider = HostServices().BuildServiceProvider();

        provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();
    }

    // Flow 3 FIX3 EK: the settle window's upper edge itself is accepted.
    [Fact]
    public async Task A_settle_window_of_exactly_25_minutes_passes_the_start_up_validation()
    {
        await using var provider = HostServices(new()
        {
            ["ProductIdentityWorkflowWorker:Enabled"] = "false",
            ["ProductIdentityWorkflowWorker:StartSettleSeconds"] = "1500"
        }).BuildServiceProvider();

        provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();
    }

    [Theory]
    [InlineData(0, 0, 5, false)] // a zero ceiling is no ceiling, even when the retry delay does not exceed it
    [InlineData(0, 1, 5, true)]
    [InlineData(30, 86_400, 1_000, true)]
    [InlineData(30, 86_401, 5, false)]
    [InlineData(30, 900, 0, false)]
    [InlineData(30, 900, 1_001, false)]
    [InlineData(901, 900, 5, false)]
    public void The_service_fault_window_has_exact_bounds(int retryDelay, int ceiling, int threshold, bool valid)
    {
        Assert.Equal(valid, ProductIdentityWorkflowWorkerOptions.IsServiceFaultWindowValid(retryDelay, ceiling, threshold));
    }

    [Theory]
    [InlineData(0, 5, 30)]
    [InlineData(900, 0, 30)]
    [InlineData(20, 5, 30)]
    public void An_enabled_worker_refuses_a_wrong_service_fault_window(int ceiling, int threshold, int retryDelay)
    {
        var options = new ProductIdentityWorkflowWorkerOptions
        {
            Enabled = true,
            LeaseOwner = "worker",
            RetryDelaySeconds = retryDelay,
            ServiceFaultMaxRetryDelaySeconds = ceiling,
            ServiceFaultErrorAfterAttempts = threshold
        };

        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID",
            Assert.Throws<InvalidOperationException>(options.EnsureValidWhenEnabled).Message);
        new ProductIdentityWorkflowWorkerOptions { Enabled = true, LeaseOwner = "worker" }.EnsureValidWhenEnabled();
    }

    [Fact]
    public void Program_registers_the_processor_only_through_the_tested_seam()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        // Code only: a call that sits in a comment is not a call.
        var program = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(Path.Combine(root!.FullName, "services", "Diten.MdmService", "src",
                "Diten.MdmService.Api", "Program.cs")),
            @"/\*.*?\*/|//[^\n]*", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.Equal(1, Count(program, "builder.Services.AddGlobalProductIdentityWorkflowProcessor(builder.Configuration);"));
        // The worker options are bound in the seam and nowhere else in the host.
        Assert.Equal(0, Count(program, "Configure<ProductIdentityWorkflowWorkerOptions>"));
        Assert.Equal(1, Count(program, "AddOptions<ProductIdentityWorkflowWorkerOptions>()"));
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
        // No binding of the worker options here: the seam the host calls does it, and that is what is under test.
        services.AddGlobalProductIdentityWorkflowProcessor(configuration);
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
