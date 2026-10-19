using System.Security.Cryptography;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-VW-W4-INFRA — the verified GSKU / market provisioning works in local Development on the REAL files:
/// (1) the two LOCKED catalogs are LF on disk and their WORKING-TREE bytes hash to the fingerprints pinned in code
///     (a Windows CRLF checkout broke this; .gitattributes now pins them to LF);
/// (2) Platform's appsettings.Development.json, bound as the host binds it, makes the production eligibility classes
///     answer ELIGIBLE (provisioning, enumeration; market with the dev script's exact process values) — reference tenant ≠
///     consumer tenant (97c5);
/// (3) the MDM ↔ Platform credential agrees (identifier, consumer service, audience, ≤ 2 s timeout) and NO secret is
///     committed: the secret lives in dotnet user-secrets (scripts/dev/set-verified-reference-dev-credential.ps1);
/// (4) outside Development the provisioning is refused (the live path is a separate decision — operator guide).
/// </summary>
[Collection(VerifiedMarketOperationalEnvironmentCollection.Name)] // the market tests set process environment variables
public sealed class VerifiedReferenceDevProvisioningConfigTests
{
    private static readonly Guid DevConsumerTenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    [Theory]
    [InlineData(VerifiedGskuOperationalProvisioningOptions.LockedCatalogFileName, VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName, VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint)]
    public void Locked_catalog_working_tree_bytes_are_lf_and_match_the_pinned_fingerprint(string fileName, string fingerprint)
    {
        var bytes = File.ReadAllBytes(BusinessReferenceDataTestHarness.GetSeedPath(fileName));
        Assert.DoesNotContain((byte)'\r', bytes); // a CRLF checkout changes the bytes, so the fingerprint can never match
        Assert.Equal(fingerprint, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

        var attributes = File.ReadAllLines(Path.Combine(RepoRoot(), ".gitattributes"));
        Assert.Contains(attributes, line =>
            line.StartsWith("services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/" + fileName + " ", StringComparison.Ordinal)
            && line.Contains("eol=lf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Development_config_makes_gsku_provisioning_enumeration_eligible_and_market_waits_for_process_provenance()
    {
        var configuration = PlatformDevelopmentConfiguration();
        var provider = Bind<BusinessReferenceDataProviderOptions>(configuration, BusinessReferenceDataProviderOptions.SectionName);
        var gsku = Bind<VerifiedGskuOperationalProvisioningOptions>(configuration, VerifiedGskuOperationalProvisioningOptions.SectionName);
        var market = Bind<VerifiedMarketOperationalProvisioningOptions>(configuration, VerifiedMarketOperationalProvisioningOptions.SectionName);

        Assert.True(provider.TryGetReferenceTenantId(out var referenceTenant));
        Assert.Equal(DevConsumerTenant, gsku.ConsumerTenantId);
        Assert.NotEqual(DevConsumerTenant, referenceTenant); // MOD-0048-FU01: the consumer cannot be the reference tenant
        Assert.True(gsku.Enabled && gsku.EnumerationEnabled && market.Enabled);
        Assert.Equal(VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion, gsku.ExpectedCatalogVersion);
        Assert.Equal(VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint, gsku.ExpectedCatalogFingerprint);
        Assert.Equal(VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion, market.ExpectedCatalogVersion);
        Assert.Equal(VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint, market.ExpectedCatalogFingerprint);

        // the host runs from the API project directory (dotnet run): resolve the relative catalog paths from there
        gsku.CatalogPath = Path.Combine(PlatformApiDirectory(), gsku.CatalogPath);
        market.CatalogPath = Path.Combine(PlatformApiDirectory(), market.CatalogPath);

        var gskuEligibility = new DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility(
            Environment("Development"), Options.Create(gsku), Options.Create(provider));
        var provisioning = await gskuEligibility.EvaluateAsync();
        Assert.True(provisioning.IsEligible, provisioning.ReasonCode);
        Assert.Equal(referenceTenant, provisioning.Facts!.ReferenceTenantId);
        Assert.Equal(DevConsumerTenant, provisioning.Facts.ConsumerTenantId);
        var enumeration = await gskuEligibility.EvaluateEnumerationAsync();
        Assert.True(enumeration.IsEligible, enumeration.ReasonCode);

        // WP-LANE-MAIN-SYNC-04, owner decision A (2026-10-14): the lane's market rule stays — the one-shot market provisioning
        // runs only when the process itself names the values (the BusinessReferenceData__VerifiedMarketOperationalProvisioning__*
        // environment variables equal to the configuration), never on appsettings alone (lane 6e3013612). The Development
        // configuration is valid; without the process provenance it is refused with that reason, not with "configuration invalid".
        var marketDecision = await new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            Environment("Development"), configuration, Options.Create(market), Options.Create(provider), Options.Create(gsku)).EvaluateAsync();
        Assert.False(marketDecision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_PROCESS_PROVENANCE_REQUIRED", marketDecision.ReasonCode);

        // the live path: the same configuration is refused outside Development (a separate design decision)
        Assert.False((await new DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility(
            Environment("Production"), Options.Create(gsku), Options.Create(provider)).EvaluateAsync()).IsEligible);
        Assert.False((await new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            Environment("Production"), configuration, Options.Create(market), Options.Create(provider), Options.Create(gsku)).EvaluateAsync()).IsEligible);
    }

    /// <summary>
    /// WP-PLT-MARKET-REFTENANT-01 (BL-645, MOD-0048-FU01 amendment 2026-10-15) — with the process values the dev script sets
    /// (read from appsettings.Development.json, equal to the configuration the operational mode builds: JSON + environment),
    /// the Development configuration makes the market provisioning ELIGIBLE again, under the configured reference tenant.
    /// </summary>
    [Fact]
    public async Task Development_config_with_the_scripts_exact_process_values_makes_market_provisioning_eligible()
    {
        var json = PlatformDevelopmentConfiguration();
        var processValues = ScriptProcessValues(json);
        using var environment = TemporaryProcessEnvironment.Apply(processValues);
        // As VerifiedMarketOperationalMode builds it: the JSON files, then the process environment over them.
        var configuration = new ConfigurationBuilder().AddConfiguration(json).AddEnvironmentVariables().Build();
        var provider = Bind<BusinessReferenceDataProviderOptions>(configuration, BusinessReferenceDataProviderOptions.SectionName);
        var gsku = Bind<VerifiedGskuOperationalProvisioningOptions>(configuration, VerifiedGskuOperationalProvisioningOptions.SectionName);
        var market = Bind<VerifiedMarketOperationalProvisioningOptions>(configuration, VerifiedMarketOperationalProvisioningOptions.SectionName);
        // the command runs from the API project directory: the file check resolves the (unchanged) relative path from there
        market.CatalogPath = Path.Combine(PlatformApiDirectory(), market.CatalogPath);

        var decision = await new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            Environment("Development"), configuration, Options.Create(market), Options.Create(provider), Options.Create(gsku)).EvaluateAsync();

        Assert.True(decision.IsEligible, decision.ReasonCode);
        Assert.True(provider.TryGetReferenceTenantId(out var referenceTenant));
        Assert.Equal(referenceTenant, decision.Facts!.ReferenceTenantId);
        Assert.NotEqual(DevConsumerTenant, referenceTenant);
        Assert.Equal(VerifiedMarketOperationalProvisioningOptions.LockedActorId, decision.Facts.ActorId);
    }

    /// <summary>The script names exactly the eight process values the eligibility reads, takes them from
    /// appsettings.Development.json (never a .local.json) and restores the process environment in a finally block.</summary>
    [Fact]
    public void The_dev_script_sets_exactly_the_eligibility_process_values_from_appsettings_and_restores_them()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "dev", "run-verified-market-provisioning.ps1"));
        // the CODE only: the comment-based help block and full-line comments may name what the script does NOT read
        var script = System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(source, @"<#[\s\S]*?#>", string.Empty),
            @"(?m)^\s*#.*$", string.Empty);
        var named = System.Text.RegularExpressions.Regex.Matches(script, @"'(BusinessReferenceData__[A-Za-z_]+)'\s*=")
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(EligibilityProcessKeys.Order(StringComparer.Ordinal).ToArray(), named);
        Assert.Contains("appsettings.Development.json", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".local.json", script, StringComparison.Ordinal);
        Assert.Contains("ConvertFrom-Json", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.Ordinal);
        Assert.True(script.IndexOf("--run-verified-market-provisioning", StringComparison.Ordinal)
                    > script.IndexOf("Set-Item -Path \"env:$name\"", StringComparison.Ordinal)); // values first, then the command
    }

    private static readonly string[] EligibilityProcessKeys =
    [
        VerifiedMarketOperationalProvisioningOptions.EnabledEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.CatalogPathEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.CatalogVersionEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.CatalogFingerprintEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.ActorIdEnvironmentKey,
        VerifiedMarketOperationalProvisioningOptions.IdempotencyNamespaceEnvironmentKey
    ];

    // What scripts/dev/run-verified-market-provisioning.ps1 sets: two fixed values and six read from the JSON configuration.
    private static Dictionary<string, string?> ScriptProcessValues(IConfiguration json)
    {
        var market = VerifiedMarketOperationalProvisioningOptions.SectionName;
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [VerifiedMarketOperationalProvisioningOptions.EnabledEnvironmentKey] = "true",
            [VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey] = "false",
            [VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey] = json[$"{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId"],
            [VerifiedMarketOperationalProvisioningOptions.CatalogPathEnvironmentKey] = json[$"{market}:CatalogPath"],
            [VerifiedMarketOperationalProvisioningOptions.CatalogVersionEnvironmentKey] = json[$"{market}:ExpectedCatalogVersion"],
            [VerifiedMarketOperationalProvisioningOptions.CatalogFingerprintEnvironmentKey] = json[$"{market}:ExpectedCatalogFingerprint"],
            [VerifiedMarketOperationalProvisioningOptions.ActorIdEnvironmentKey] = json[$"{market}:ActorId"],
            [VerifiedMarketOperationalProvisioningOptions.IdempotencyNamespaceEnvironmentKey] = json[$"{market}:IdempotencyNamespace"]
        };
    }

    [Fact]
    public void Mdm_and_platform_credential_agree_and_no_secret_is_committed()
    {
        var platform = Bind<VerifiedGskuResolverCredentialOptions>(PlatformDevelopmentConfiguration(), VerifiedGskuResolverCredentialOptions.SectionName).Mdm;
        Assert.False(string.IsNullOrWhiteSpace(platform.Identifier));
        Assert.Equal(VerifiedGskuResolverCredentialAuthenticator.ConsumerService, platform.ConsumerService);
        Assert.Equal(VerifiedGskuResolverCredentialAuthenticator.Audience, platform.AllowedAudience);
        Assert.True(string.IsNullOrEmpty(platform.ActiveSecret)); // user-secrets only

        var mdm = new ConfigurationBuilder().AddJsonFile(Path.Combine(MdmApiDirectory(), "appsettings.Development.json")).Build()
            .GetSection("VerifiedGskuResolver");
        Assert.Equal(platform.Identifier, mdm["CredentialIdentifier"]);
        Assert.Null(mdm["CredentialSecret"]); // user-secrets only
        Assert.True(Uri.TryCreate(mdm["PlatformBaseAddress"], UriKind.Absolute, out var platformAddress));
        Assert.Equal(5057, platformAddress!.Port);
        var timeout = TimeSpan.Parse(mdm["Timeout"]!);
        Assert.True(timeout > TimeSpan.Zero && timeout <= TimeSpan.FromSeconds(2)); // the MDM clients refuse anything else

        // the secret is loaded only when the project carries a UserSecretsId
        Assert.Contains("<UserSecretsId>", File.ReadAllText(Path.Combine(MdmApiDirectory(), "Diten.MdmService.Api.csproj")));
        Assert.Contains("<UserSecretsId>", File.ReadAllText(Path.Combine(PlatformApiDirectory(), "Diten.Platform.API.csproj")));

        // no committed file carries the verified-reference secret (other credentials in these files are not in scope)
        foreach (var file in Directory.GetFiles(PlatformApiDirectory(), "appsettings*.json")
                     .Concat(Directory.GetFiles(MdmApiDirectory(), "appsettings*.json")))
        {
            var one = new ConfigurationBuilder().AddJsonFile(file).Build();
            Assert.Null(one["VerifiedGskuResolverCredential:Mdm:ActiveSecret"]);
            Assert.Null(one["VerifiedGskuResolverCredential:Mdm:PreviousSecret"]);
            Assert.Null(one["VerifiedGskuResolver:CredentialSecret"]);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private static IConfigurationRoot PlatformDevelopmentConfiguration()
        => new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(PlatformApiDirectory(), "appsettings.json"))
            .AddJsonFile(Path.Combine(PlatformApiDirectory(), "appsettings.Development.json"))
            .Build();

    private static T Bind<T>(IConfiguration configuration, string section) where T : new()
    {
        var value = new T();
        configuration.GetSection(section).Bind(value);
        return value;
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private static string PlatformApiDirectory()
        => Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    private static string MdmApiDirectory()
        => Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Api");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (AGENTS.md) not found.");
    }
}
