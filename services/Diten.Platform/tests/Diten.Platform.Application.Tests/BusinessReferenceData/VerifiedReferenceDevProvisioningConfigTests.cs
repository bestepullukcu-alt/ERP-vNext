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
///     answer ELIGIBLE (provisioning, enumeration, market) — reference tenant ≠ consumer tenant (97c5);
/// (3) the MDM ↔ Platform credential agrees (identifier, consumer service, audience, ≤ 2 s timeout) and NO secret is
///     committed: the secret lives in dotnet user-secrets (scripts/dev/set-verified-reference-dev-credential.ps1);
/// (4) outside Development the provisioning is refused (the live path is a separate decision — operator guide).
/// </summary>
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
    public async Task Development_config_makes_gsku_provisioning_enumeration_and_market_eligible()
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

        var marketDecision = await new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            Environment("Development"), Options.Create(market), Options.Create(provider)).EvaluateAsync();
        Assert.True(marketDecision.IsEligible, marketDecision.ReasonCode);

        // the live path: the same configuration is refused outside Development (a separate design decision)
        Assert.False((await new DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility(
            Environment("Production"), Options.Create(gsku), Options.Create(provider)).EvaluateAsync()).IsEligible);
        Assert.False((await new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            Environment("Production"), Options.Create(market), Options.Create(provider)).EvaluateAsync()).IsEligible);
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

    private static IConfiguration PlatformDevelopmentConfiguration()
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
