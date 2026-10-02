extern alias PlatformApi;

using System.Text.RegularExpressions;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using AuthLocalSettings = Diten.AuthService.Api.Configuration.DevelopmentLocalSettings;
using PlatformLocalSettings = PlatformApi::Diten.Platform.API.Configuration.DevelopmentLocalSettings;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.3 — the Development-only, git-ignored local settings file: where it sits in the configuration
/// chain, that nothing but Development loads it, that it can be switched OFF (and only off), that it is not loaded
/// at all when its place under the environment variables cannot be found, and that it never travels into build or
/// publish output.
/// </summary>
public sealed class DevelopmentLocalSettingsTests
{
    /// <summary>The Auth and the Platform copy of the helper, each exercised for real (MDM's is text-identical).</summary>
    private static void Add(string service, IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        if (service == "Auth") AuthLocalSettings.AddDevelopmentLocalSettings(configuration, environment);
        else if (service == "Platform") PlatformLocalSettings.AddDevelopmentLocalSettings(configuration, environment);
        else throw new ArgumentOutOfRangeException(nameof(service));
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void InDevelopment_TheLocalFileOverridesTheTrackedDevelopmentFile_AndNothingLaterInTheChain(string service)
    {
        using var contentRoot = new ScratchDirectory();
        File.WriteAllText(contentRoot.File("appsettings.json"), """{ "A": "base", "B": "base", "C": "base", "D": "base" }""");
        File.WriteAllText(contentRoot.File("appsettings.Development.json"), """{ "B": "tracked-dev", "C": "tracked-dev", "D": "tracked-dev" }""");
        File.WriteAllText(contentRoot.File(AuthLocalSettings.FileName), """{ "C": "local", "D": "local" }""");
        var configuration = Chain(contentRoot.Path, laterSource: new() { ["D"] = "environment-or-command-line" });

        Add(service, configuration, new Environment("Development", contentRoot.Path));

        Assert.Equal("base", configuration["A"]);
        Assert.Equal("tracked-dev", configuration["B"]);
        Assert.Equal("local", configuration["C"]); // over the tracked Development file
        Assert.Equal("environment-or-command-line", configuration["D"]); // under everything that comes after it
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void OutsideDevelopment_TheLocalFileIsNotLoaded(string service)
    {
        using var contentRoot = new ScratchDirectory();
        File.WriteAllText(contentRoot.File("appsettings.json"), """{ "C": "base" }""");
        File.WriteAllText(contentRoot.File(AuthLocalSettings.FileName), """{ "C": "local" }""");

        foreach (var name in new[] { "Production", "Staging", "development-like" })
        {
            var configuration = Chain(contentRoot.Path, laterSource: []);
            Add(service, configuration, new Environment(name, contentRoot.Path));
            Assert.Equal("base", configuration["C"]);
        }

    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void InDevelopment_AnAbsentLocalFileIsNormal(string service)
    {
        using var contentRoot = new ScratchDirectory();
        File.WriteAllText(contentRoot.File("appsettings.json"), """{ "C": "base" }""");
        var configuration = Chain(contentRoot.Path, laterSource: []);

        Add(service, configuration, new Environment("Development", contentRoot.Path));

        Assert.Equal("base", configuration["C"]);
    }

    // ── the OFF switch ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Auth", "false")]
    [InlineData("Auth", "False")]
    [InlineData("Auth", "0")]      // anything that is not "true" is OFF — a switch that fails towards "not loaded"
    [InlineData("Auth", "off")]
    [InlineData("Auth", "")]
    [InlineData("Platform", "false")]
    [InlineData("Platform", "no")]
    public void InDevelopment_WhenSwitchedOff_TheLocalFileIsNotLoaded(string service, string value)
    {
        using var contentRoot = DevelopmentContentRoot();
        var configuration = Chain(contentRoot.Path, laterSource: new() { [AuthLocalSettings.EnabledKey] = value });

        Add(service, configuration, new Environment("Development", contentRoot.Path));

        Assert.Equal("tracked-dev", configuration["C"]);
        Assert.False(HasLocalSource(configuration), "the local settings file became a configuration source");
    }

    [Theory]
    [InlineData("Auth", "true")]
    [InlineData("Auth", "True")]
    [InlineData("Platform", "true")]
    public void InDevelopment_WhenTheSwitchSaysOn_TheLocalFileIsLoadedAsWithoutTheSwitch(string service, string value)
    {
        using var contentRoot = DevelopmentContentRoot();
        var configuration = Chain(contentRoot.Path, laterSource: new() { [AuthLocalSettings.EnabledKey] = value });

        Add(service, configuration, new Environment("Development", contentRoot.Path));

        Assert.Equal("local", configuration["C"]);
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void OutsideDevelopment_TheSwitchCannotTurnTheLocalFileOn(string service)
    {
        using var contentRoot = DevelopmentContentRoot();

        foreach (var name in new[] { "Production", "Staging", "development-like", "Test" })
        {
            var configuration = Chain(contentRoot.Path, laterSource: new() { [AuthLocalSettings.EnabledKey] = "true" });
            Add(service, configuration, new Environment(name, contentRoot.Path));
            Assert.Equal("tracked-dev", configuration["C"]); // what the chain held before; the local value never appears
            Assert.False(HasLocalSource(configuration), name + ": the switch loaded the local file outside Development");
        }
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void InDevelopment_APlainConfigurationBuilder_IsReadForTheSwitchToo(string service)
    {
        using var contentRoot = DevelopmentContentRoot();
        IConfigurationBuilder Plain(Dictionary<string, string?> later) => new ConfigurationBuilder()
            .SetBasePath(contentRoot.Path)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddInMemoryCollection(later);

        var off = Plain(new() { [AuthLocalSettings.EnabledKey] = "false" });
        Add(service, off, new Environment("Development", contentRoot.Path));
        Assert.Equal("tracked-dev", off.Build()["C"]);

        var on = Plain([]);
        Add(service, on, new Environment("Development", contentRoot.Path));
        Assert.Equal("local", on.Build()["C"]);
    }

    // ── the latent case: no tracked Development file among the sources ────────────────────────────────────────

    /// <summary>
    /// Before: with no <c>appsettings.Development.json</c> source to sit behind, the local file was appended LAST —
    /// above the environment variables and the command line. Now its place cannot be found, so it is not loaded.
    /// </summary>
    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void InDevelopment_WhenTheTrackedDevelopmentFileIsNotASource_TheLocalFileIsNotLoaded_AndOutranksNothing(string service)
    {
        using var contentRoot = DevelopmentContentRoot();
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(contentRoot.Path);
        configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["D"] = "environment-or-command-line" });

        Add(service, configuration, new Environment("Development", contentRoot.Path));

        Assert.Equal("environment-or-command-line", configuration["D"]); // the local file's D did not override it
        Assert.Equal("base", configuration["C"]);
        Assert.False(HasLocalSource(configuration), "the local settings file was appended after the last source");
    }

    // ── the real host builder: real environment variables, real command line ──────────────────────────────────

    /// <summary>
    /// <c>WebApplication.CreateBuilder</c> itself — the builder Program.cs hands to the helper — with a scratch
    /// content root: the local file sits over the tracked Development file and UNDER a real process environment
    /// variable and a real command-line argument, and each of those two can switch it off. (The command line is
    /// also how <c>WebApplicationFactory.UseSetting</c> reaches a minimal-hosting Program.)
    /// </summary>
    [Theory]
    [InlineData("Auth")]
    [InlineData("Platform")]
    public void InARealDevelopmentBuilder_EnvironmentAndCommandLineOutrankTheLocalFile_AndEitherCanSwitchItOff(string service)
    {
        using var contentRoot = new ScratchDirectory();
        File.WriteAllText(contentRoot.File("appsettings.json"),
            """{ "LocalSettingsProbe": { "Tracked": "base", "Local": "base", "Environment": "base", "CommandLine": "base" } }""");
        File.WriteAllText(contentRoot.File("appsettings.Development.json"),
            """{ "LocalSettingsProbe": { "Tracked": "tracked-dev", "Local": "tracked-dev", "Environment": "tracked-dev", "CommandLine": "tracked-dev" } }""");
        File.WriteAllText(contentRoot.File(AuthLocalSettings.FileName),
            """{ "LocalSettingsProbe": { "Local": "local", "Environment": "local", "CommandLine": "local" } }""");

        ConfigurationManager Build(params string[] arguments)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ContentRootPath = contentRoot.Path,
                // An assembly without a user-secrets id: the Development default (AddUserSecrets) finds nothing to read.
                ApplicationName = typeof(DevelopmentLocalSettingsTests).Assembly.GetName().Name,
                Args = ["--hostBuilder:reloadConfigOnChange=false", "--LocalSettingsProbe:CommandLine=command-line", .. arguments]
            });
            Add(service, builder.Configuration, builder.Environment);
            return builder.Configuration;
        }

        const string probeVariable = "LocalSettingsProbe__Environment";
        const string switchVariable = "DevelopmentLocalSettings__Enabled";
        var previousProbe = System.Environment.GetEnvironmentVariable(probeVariable);
        var previousSwitch = System.Environment.GetEnvironmentVariable(switchVariable);
        try
        {
            System.Environment.SetEnvironmentVariable(probeVariable, "environment");
            System.Environment.SetEnvironmentVariable(switchVariable, null);

            using (var loaded = Build())
            {
                Assert.Equal("tracked-dev", loaded["LocalSettingsProbe:Tracked"]);
                Assert.Equal("local", loaded["LocalSettingsProbe:Local"]);                 // over the tracked file
                Assert.Equal("environment", loaded["LocalSettingsProbe:Environment"]);     // under the environment
                Assert.Equal("command-line", loaded["LocalSettingsProbe:CommandLine"]);    // under the command line
            }

            using (var offByCommandLine = Build("--DevelopmentLocalSettings:Enabled=false"))
            {
                Assert.Equal("tracked-dev", offByCommandLine["LocalSettingsProbe:Local"]);
                Assert.False(HasLocalSource(offByCommandLine));
            }

            System.Environment.SetEnvironmentVariable(switchVariable, "false");
            using (var offByEnvironment = Build())
            {
                Assert.Equal("tracked-dev", offByEnvironment["LocalSettingsProbe:Local"]);
                Assert.False(HasLocalSource(offByEnvironment));
            }
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(probeVariable, previousProbe);
            System.Environment.SetEnvironmentVariable(switchVariable, previousSwitch);
        }
    }

    // ── the test hosts that run a real Program under Development ──────────────────────────────────────────────

    /// <summary>
    /// A test host that runs a real Program under Development from the real project directory would load whatever
    /// a developer bootstrapped there. Every test source in the three services that builds such a host — a
    /// <c>WebApplicationFactory</c>, or a child process given <c>ASPNETCORE_ENVIRONMENT=Development</c> — must set
    /// the switch off, or be listed below with the reason it does not need to.
    /// </summary>
    [Fact]
    public void EveryTestHostThatRunsARealProgramUnderDevelopment_SwitchesTheLocalFileOff()
    {
        var root = DevTrustBootstrapTests.RepositoryRoot();
        var notNeeded = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalBootstrapTests.cs"] =
                "the child gets only Section E operational arguments (Program.cs returns before the web builder exists), and its working directory is the test output directory, where the local file never is",
            ["services/Diten.AuthService/tests/Diten.AuthService.AccountKindAcceptanceHost.Tests/PoisonedEnvironmentTests.cs"] =
                "the Development values are poison the acceptance host must NOT pass on; its API child runs as AcceptanceHost",
            ["services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Audit/SelectedAuditIntentDeliveryContractTests.cs"] =
                "the child runs the selected one-shot path (Program.cs returns before the web builder exists), and its working directory is the test output directory, where the local file never is"
        };
        var hostsARealProgram = new Regex(
            """new\s+WebApplicationFactory\s*<|\[\s*"ASPNETCORE_ENVIRONMENT"\s*\]\s*=\s*(?:Environments\.Development|"Development")""");
        var switchedOff = new Regex("""\[\s*"DevelopmentLocalSettings__Enabled"\s*\]\s*=\s*"false"|UseSetting\(\s*"DevelopmentLocalSettings:Enabled"\s*,\s*"false"\s*\)""");

        var found = new List<string>();
        foreach (var tests in new[] { "services/Diten.AuthService/tests", "services/Diten.Platform/tests", "services/Diten.MdmService/tests" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, tests), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal)) continue;
                if (relative.EndsWith("/" + nameof(DevelopmentLocalSettingsTests) + ".cs", StringComparison.Ordinal)) continue;
                var source = File.ReadAllText(file);
                if (!hostsARealProgram.IsMatch(source)) continue;
                found.Add(relative);
                if (notNeeded.ContainsKey(relative)) continue;
                Assert.True(switchedOff.IsMatch(source), relative + " hosts a real Program under Development and does not switch the local settings file off");
            }
        }

        // The scan sees the hosts it is about (a scanner that matches nothing would pass for the wrong reason)…
        Assert.Contains("services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Testing/AccountKindAcceptance.cs", found);
        Assert.Contains("services/Diten.Platform/tests/Diten.Platform.BackgroundJobs.Tests/PlatformApiStartupExecutionModeTests.cs", found);
        Assert.Contains("services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ApiStartupExecutionModeTests.cs", found);
        // …and an exemption that no longer applies is removed, not left behind.
        Assert.All(notNeeded.Keys, exempt => Assert.Contains(exempt, found));
    }

    [Fact]
    public void TheThreeServicesCarryTheSameHelper_AndCallItRightAfterTheBuilderIsCreated()
    {
        var root = DevTrustBootstrapTests.RepositoryRoot();
        var services = new[]
        {
            "services/Diten.AuthService/src/Diten.AuthService.Api",
            "services/Diten.Platform/src/Diten.Platform.API",
            "services/Diten.MdmService/src/Diten.MdmService.Api"
        };

        // Identical apart from the namespace line: the MDM copy is not referenced by this project, so the two
        // behaviour-tested copies above stand for it only as long as its text is the same.
        var bodies = services.Select(service => Regex.Replace(
            File.ReadAllText(Path.Combine(root, service, "Configuration", "DevelopmentLocalSettings.cs")),
            @"^namespace [\w.]+;\r?$", "namespace X;", RegexOptions.Multiline).Replace("\r", "")).ToArray();
        Assert.Equal(bodies[0], bodies[1]);
        Assert.Equal(bodies[0], bodies[2]);

        foreach (var service in services)
        {
            var program = File.ReadAllText(Path.Combine(root, service, "Program.cs"));
            var created = program.IndexOf("var builder = WebApplication.CreateBuilder(args);", StringComparison.Ordinal);
            var loaded = program.IndexOf("DevelopmentLocalSettings.AddDevelopmentLocalSettings(builder.Configuration, builder.Environment);", StringComparison.Ordinal);
            var firstRegistration = program.IndexOf("builder.Services.", StringComparison.Ordinal);
            Assert.True(created >= 0 && loaded > created, service + ": the local settings are loaded after the builder is created");
            Assert.True(loaded < firstRegistration, service + ": and before any service reads configuration");
            Assert.DoesNotContain("AddJsonFile(\"appsettings.Development.local.json\"", program, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheLocalFileIsExcludedFromBuildAndPublishOutput_AndFromGit()
    {
        var root = DevTrustBootstrapTests.RepositoryRoot();
        foreach (var project in new[]
                 {
                     "services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj",
                     "services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj",
                     "services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj"
                 })
        {
            var text = File.ReadAllText(Path.Combine(root, project));
            Assert.Contains("<Content Remove=\"appsettings.Development.local.json\" />", text, StringComparison.Ordinal);
            Assert.Contains("<None Remove=\"appsettings.Development.local.json\" />", text, StringComparison.Ordinal);
        }

        var ignored = File.ReadAllLines(Path.Combine(root, ".gitignore"));
        Assert.Contains("appsettings.Development.local.json", ignored);
        Assert.Contains("appsettings.Development.local.json.*.tmp", ignored);
    }

    /// <summary>A content root with the base, the tracked Development and the local file, each giving C its own value.</summary>
    private static ScratchDirectory DevelopmentContentRoot()
    {
        var contentRoot = new ScratchDirectory();
        File.WriteAllText(contentRoot.File("appsettings.json"), """{ "C": "base", "D": "base" }""");
        File.WriteAllText(contentRoot.File("appsettings.Development.json"), """{ "C": "tracked-dev" }""");
        File.WriteAllText(contentRoot.File(AuthLocalSettings.FileName), """{ "C": "local", "D": "local" }""");
        return contentRoot;
    }

    private static bool HasLocalSource(IConfigurationBuilder configuration) =>
        configuration.Sources.OfType<JsonConfigurationSource>().Any(source =>
            string.Equals(source.Path, AuthLocalSettings.FileName, StringComparison.OrdinalIgnoreCase));

    /// <summary>appsettings.json → appsettings.{Environment}.json → a later source (environment / command line).</summary>
    private static ConfigurationManager Chain(string contentRoot, Dictionary<string, string?> laterSource)
    {
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(contentRoot);
        configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
        configuration.AddInMemoryCollection(laterSource);
        return configuration;
    }

    private sealed class Environment(string name, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "local-settings-test";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }
}
