using Diten.PvgService.Api;
using Diten.PvgService.Application.RegPvBase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.PvgService.Api.Tests;

// MOD-0230 DL-3 part D (AC-10): a production-like start with Pvg:Runtime:OperationalRuntimeAuthorized=true is refused
// unless Pvg:Runtime:ApprovalReference holds a well-formed runtime_approval_id (RTA-MOD0230-NNN, RTA-01). The host
// cannot know whether the record exists in the repository; that is the CI scan's job (PvgServiceApiHostTests).
// Tests never set process environment variables (xUnit classes run in parallel) and never start a server: the host
// is only configured or built.
public sealed class PvgServiceRuntimeApprovalTests
{
    private const string ReferenceKey = "Pvg:Runtime:ApprovalReference";
    private const string WellFormedReference = "RTA-MOD0230-001";
    private const string ApprovalReferenceRefusal = "approval reference";
    private const string LocalTestUrls = "http://127.0.0.1:0";

    private static readonly string[] RequiredDependencyCodes =
    [
        "PVG_DEPENDENCY_FIELD_SECURITY",
        "PVG_DEPENDENCY_WORKFLOW_GATE",
        "PVG_DEPENDENCY_EVIDENCE_LINK",
        "PVG_DEPENDENCY_PERMISSION_GATE",
        "PVG_DEPENDENCY_INTAKE_STORE"
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Production_like_authorized_start_without_approval_reference_is_refused(string? reference)
    {
        var configuration = AuthorizedConfiguration(reference);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPvgServiceApiHost(configuration, new TestHostEnvironment(Environments.Production)));
        Assert.Contains(ApprovalReferenceRefusal, exception.Message, StringComparison.OrdinalIgnoreCase);

        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            Environments.Production,
            configuration,
            LocalTestUrls);
        var buildException = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains(ApprovalReferenceRefusal, buildException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("RTA-MOD0230-1")]
    [InlineData("RTA-MOD0230-0001")]
    [InlineData("rta-mod0230-001")]
    [InlineData("RTA-MOD0231-001")]
    [InlineData(" RTA-MOD0230-001")]
    [InlineData("RTA-MOD0230-001 ")]
    [InlineData("RTA-MOD0230-٠٠١")] // Arabic-Indic digits: \d would accept them, [0-9] must not.
    [InlineData("RTA-MOD0230-001\n")] // `$` would accept a trailing line feed; the shape is matched whole.
    public void Production_like_authorized_start_with_malformed_approval_reference_is_refused(string reference)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPvgServiceApiHost(
                AuthorizedConfiguration(reference),
                new TestHostEnvironment(Environments.Production)));

        Assert.Contains(ApprovalReferenceRefusal, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(reference, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_like_authorized_start_with_well_formed_approval_reference_passes_the_approval_check()
    {
        var services = new ServiceCollection();

        services.AddPvgServiceApiHost(
            AuthorizedConfiguration(WellFormedReference),
            new TestHostEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(provider.GetService<IPvgFieldSecurityPolicy>());
        Assert.Null(provider.GetService<IPvgWorkflowTransitionGate>());
        Assert.Null(provider.GetService<IPvgEvidenceLinkPort>());
        Assert.Null(provider.GetService<IPvgPermissionGate>());
        Assert.Null(provider.GetService<IPvgIntakeDraftStore>());
    }

    [Fact]
    public void Non_production_start_does_not_require_approval_reference()
    {
        var exception = Record.Exception(() =>
            new ServiceCollection().AddPvgServiceApiHost(
                AuthorizedConfiguration(reference: null),
                new TestHostEnvironment(Environments.Development)));

        Assert.Null(exception);
    }

    [Fact]
    public void Approval_reference_check_follows_the_existing_refusals()
    {
        // The approval reference rule only runs when the flag is true, so this row measures that the first rule is kept.
        var notAuthorized = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPvgServiceApiHost(
                new ConfigurationBuilder().Build(),
                new TestHostEnvironment(Environments.Production)));
        Assert.Contains("operational runtime is not authorized", notAuthorized.Message, StringComparison.OrdinalIgnoreCase);

        // This row measures the order: the adapter refusal must win over the approval reference refusal.
        var nonProductionAdapters = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPvgServiceApiHost(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true",
                        ["Pvg:Runtime:UseNonProductionAdapters"] = "true"
                    })
                    .Build(),
                new TestHostEnvironment(Environments.Production)));
        Assert.Contains("cannot use non-production adapters", nonProductionAdapters.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_reads_approval_reference_from_configuration()
    {
        string[] productionLikeAuthorized =
        [
            "--Pvg:Runtime:ProductionLike=true",
            "--Pvg:Runtime:OperationalRuntimeAuthorized=true",
            "--Pvg:Runtime:UseNonProductionAdapters=false"
        ];

        var withoutReference = Assert.Throws<InvalidOperationException>(() =>
            Program.CreateWebHostBuilder(productionLikeAuthorized).Build());
        Assert.Contains(ApprovalReferenceRefusal, withoutReference.Message, StringComparison.OrdinalIgnoreCase);

        var withReference = Assert.Throws<InvalidOperationException>(() =>
            Program.CreateWebHostBuilder([.. productionLikeAuthorized, "--" + ReferenceKey + "=" + WellFormedReference])
                .Build());
        foreach (var code in RequiredDependencyCodes)
        {
            Assert.Contains(code, withReference.Message, StringComparison.Ordinal);
        }
    }

    // Production-like host configuration with the flag on; the reference key is left out when reference is null.
    private static IConfiguration AuthorizedConfiguration(string? reference)
    {
        var values = new Dictionary<string, string?>
        {
            ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true"
        };

        if (reference is not null)
        {
            values[ReferenceKey] = reference;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Diten.PvgService.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
