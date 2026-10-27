using Diten.PvgService.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.PvgService.Api.Tests;

// MOD-0230 DL-3 part C: an empty ASPNETCORE_ENVIRONMENT counts as Production (owner decision K3), and Program's
// ASPNETCORE_URLS reading is pinned. Tests never set process environment variables (xUnit classes run in parallel)
// and never start a server: the host is only configured or built.
public sealed class PvgServiceHostEnvironmentTests
{
    private const string LocalDefaultUrls = "http://127.0.0.1:5011";
    private const string NotAuthorizedRefusal = "operational runtime is not authorized";

    private static readonly string[] RequiredDependencyCodes =
    [
        "PVG_DEPENDENCY_FIELD_SECURITY",
        "PVG_DEPENDENCY_WORKFLOW_GATE",
        "PVG_DEPENDENCY_EVIDENCE_LINK",
        "PVG_DEPENDENCY_PERMISSION_GATE",
        "PVG_DEPENDENCY_INTAKE_STORE"
    ];

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(null)]
    public void Blank_environment_name_is_set_as_production_on_the_host_builder(string? environmentName)
    {
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            environmentName,
            EmptyConfiguration(),
            "http://127.0.0.1:0");

        Assert.Equal(Environments.Production, builder.GetSetting(WebHostDefaults.EnvironmentKey));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData(" ", false)]
    [InlineData(" ", true)]
    public void Blank_environment_name_host_build_is_refused_like_production(
        string environmentName,
        bool operationalRuntimeAuthorized)
    {
        var configuration = operationalRuntimeAuthorized
            ? new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true"
                })
                .Build()
            : EmptyConfiguration();

        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            environmentName,
            configuration,
            "http://127.0.0.1:0");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        if (operationalRuntimeAuthorized)
        {
            foreach (var code in RequiredDependencyCodes)
            {
                Assert.Contains(code, exception.Message, StringComparison.Ordinal);
            }
        }
        else
        {
            Assert.Contains(NotAuthorizedRefusal, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Runtime_options_treat_blank_environment_name_as_production_like(string environmentName)
    {
        var options = PvgServiceApiRuntimeOptions.From(EmptyConfiguration(), environmentName);

        Assert.True(options.IsProductionLike);
        Assert.False(options.UseNonProductionAdapters);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPvgServiceApiHost(
                EmptyConfiguration(),
                new TestHostEnvironment(environmentName)));

        Assert.Contains(NotAuthorizedRefusal, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Staging", false)]
    [InlineData("Production", true)]
    [InlineData("PRODUCTION", true)]
    public void Explicit_environment_names_keep_their_meaning(string environmentName, bool expectedProductionLike)
    {
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(
            environmentName,
            EmptyConfiguration(),
            "http://127.0.0.1:0");

        Assert.Equal(environmentName, builder.GetSetting(WebHostDefaults.EnvironmentKey));

        var options = PvgServiceApiRuntimeOptions.From(EmptyConfiguration(), environmentName);

        Assert.Equal(expectedProductionLike, options.IsProductionLike);
        Assert.Equal(!expectedProductionLike, options.UseNonProductionAdapters);
    }

    [Theory]
    [InlineData("PRODUCTION")]
    [InlineData("production")]
    public void Runtime_options_treat_upper_case_production_as_production_like_under_turkish_culture(
        string environmentName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");

            var options = PvgServiceApiRuntimeOptions.From(EmptyConfiguration(), environmentName);

            Assert.True(options.IsProductionLike);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    // Compares with the current process environment; the variable is never set here (see class comment).
    [Fact]
    public void Program_urls_come_only_from_aspnetcore_urls_variable_or_the_local_default()
    {
        var variable = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

        if (variable is null)
        {
            var builder = Program.CreateWebHostBuilder([]);

            Assert.Equal(LocalDefaultUrls, builder.GetSetting(WebHostDefaults.ServerUrlsKey));
        }
        else if (!string.IsNullOrWhiteSpace(variable))
        {
            var builder = Program.CreateWebHostBuilder([]);

            Assert.Equal(variable, builder.GetSetting(WebHostDefaults.ServerUrlsKey));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => Program.CreateWebHostBuilder([]));
        }
    }

    [Fact]
    public void Program_source_reads_urls_only_from_aspnetcore_urls_variable_text()
    {
        var programPath = ProgramSourcePath();
        Assert.True(File.Exists(programPath), $"Program source not found at '{programPath}'.");

        var source = File.ReadAllText(programPath);

        Assert.Equal(
            1,
            CountMatches(
                source,
                @"\bvar\s+urls\s*=\s*Environment\s*\.\s*GetEnvironmentVariable\s*\(\s*""ASPNETCORE_URLS""\s*\)\s*\?\?\s*""http://127\.0\.0\.1:5011""\s*;"));
        Assert.Equal(
            1,
            CountMatches(source, @"\bCreatePvgServiceWebHostBuilder\s*\(\s*environmentName\s*,\s*configuration\s*,\s*urls\s*\)"));
        Assert.Equal(0, CountMatches(source, @"\bUseUrls\b"));
        Assert.Equal(0, CountMatches(source, @"""--urls"""));
        Assert.Equal(0, CountMatches(source, @"\[\s*""urls""\s*\]"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_urls_are_refused_by_the_host_builder(string urls)
    {
        Assert.Throws<ArgumentException>(() =>
            PvgServiceApiHost.CreatePvgServiceWebHostBuilder("Development", EmptyConfiguration(), urls));
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    private static int CountMatches(string code, string pattern) =>
        Regex.Matches(code, pattern, RegexOptions.CultureInvariant).Count;

    private static string ProgramSourcePath([CallerFilePath] string testSourcePath = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testSourcePath)!,
            "..",
            "..",
            "src",
            "Diten.PvgService.Api",
            "Program.cs"));

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Diten.PvgService.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
