extern alias PlatformApi;

using System.Text.RegularExpressions;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using AuthLocalSettings = Diten.AuthService.Api.Configuration.DevelopmentLocalSettings;
using PlatformLocalSettings = PlatformApi::Diten.Platform.API.Configuration.DevelopmentLocalSettings;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.3 — the Development-only, git-ignored local settings file: where it sits in the configuration
/// chain, that nothing but Development loads it, and that it never travels into build or publish output.
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
