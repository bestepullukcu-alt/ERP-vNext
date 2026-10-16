using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.1 — validation is fail-closed and happens at service REGISTRATION, i.e. before Auth's
/// persistence layer has seeded anything and before any registry write. Every test goes through the production
/// entry point (<see cref="ServiceClientRegistryStartup.AddServiceClientRegistry"/>). No database is involved.
/// </summary>
public sealed class ServiceClientRegistryValidationTests
{
    private const string SentinelValue = "SENTINEL-VALUE-THAT-MUST-NOT-BE-ECHOED";

    [Fact]
    public void Startup_WhenSectionIsAbsent_IsValidAndDeclaresNothing()
    {
        var services = Register(new Dictionary<string, string?>());

        using var provider = services.BuildServiceProvider();
        Assert.Empty(provider.GetRequiredService<ServiceClientRegistryDeclarations>().Clients);
    }

    [Fact]
    public async Task Startup_WhenSectionIsEmpty_StartsWithoutResolvingAnyPersistence()
    {
        // No IMongoDatabase, no repository is registered: an empty section must not reach for one.
        var services = Register(new Dictionary<string, string?> { ["ServiceClientRegistry:Clients"] = "" });

        await using var provider = services.BuildServiceProvider();
        foreach (var hosted in provider.GetServices<IHostedService>()) await hosted.StartAsync(CancellationToken.None);
    }

    [Fact]
    public void Startup_WhenEntryIsValid_AcceptsIt()
    {
        var tenant = Guid.NewGuid();
        var services = Register(Entry(0, tenants: [tenant.ToString("D")]));

        using var provider = services.BuildServiceProvider();
        var declared = Assert.Single(provider.GetRequiredService<ServiceClientRegistryDeclarations>().Clients);
        Assert.Equal("registry-validation-client", declared.ClientCode);
        Assert.Equal([tenant], declared.Tenants);
    }

    [Theory]
    [InlineData("ClientCode", "", "ClientCode is required")]
    [InlineData("ClientCode", " padded ", "ClientCode is malformed")]
    [InlineData("ServiceName", "   ", "ServiceName is required")]
    [InlineData("ServiceName", "Diten.Other", "ServiceName is not a service the issuer accepts")]
    [InlineData("AllowedAudience", "", "AllowedAudience is required")]
    [InlineData("AllowedAudience", "SOME_OTHER_AUDIENCE", "AllowedAudience is not an audience the issuer accepts")]
    [InlineData("CredentialHash", "", "CredentialHash is required")]
    [InlineData("CredentialHash", "not-a-hash", "CredentialHash is malformed")]
    [InlineData("CredentialHash", "pbkdf2-sha256$1000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", "CredentialHash is malformed")]
    [InlineData("CredentialVersion", "", "CredentialVersion is required")]
    [InlineData("CredentialVersion", " v1", "CredentialVersion is malformed")]
    public void Startup_WhenAFieldIsBlankOrMalformed_StopsBeforeAnyWrite_NamingIndexAndFieldOnly(
        string field, string value, string expected)
    {
        // The valid entry sits at index 0; the broken one at index 1 — the message must name index 1.
        var settings = Entry(0, clientCode: "registry-validation-first");
        foreach (var pair in Entry(1)) settings[pair.Key] = pair.Value;
        settings[$"ServiceClientRegistry:Clients:1:{field}"] = value;

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.Equal($"Configuration error: ServiceClientRegistry:Clients[1] {expected}.", exception.Message);
    }

    [Theory]
    [InlineData("ClientCode")]
    [InlineData("ServiceName")]
    [InlineData("AllowedAudience")]
    [InlineData("CredentialHash")]
    [InlineData("CredentialVersion")]
    public void Startup_WhenAFieldIsRejected_NeverEchoesItsValue(string field)
    {
        var settings = Entry(0);
        // Malformed for every field: surrounding whitespace is never an exact value, and it is no accepted name.
        settings[$"ServiceClientRegistry:Clients:0:{field}"] = " " + SentinelValue + " ";

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.DoesNotContain(SentinelValue, exception.ToString(), StringComparison.Ordinal);
        Assert.Contains("Clients[0] " + field, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "Tenants[1] is required")]
    [InlineData("not-a-guid", "Tenants[1] is malformed")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Tenants[1] is malformed")]
    [InlineData("11111111-1111-4111-8111-111111111111", "Tenants[1] is a duplicate")]
    public void Startup_WhenATenantIsBlankMalformedOrRepeated_Stops(string second, string expected)
    {
        var settings = Entry(0, tenants: ["11111111-1111-4111-8111-111111111111", second]);

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.Equal($"Configuration error: ServiceClientRegistry:Clients[0] {expected}.", exception.Message);
    }

    [Fact]
    public void Startup_WhenClientCodeIsRepeated_Stops_NamingTheSecondEntry()
    {
        var settings = Entry(0);
        foreach (var pair in Entry(1)) settings[pair.Key] = pair.Value;

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.Equal("Configuration error: ServiceClientRegistry:Clients[1] ClientCode is a duplicate.", exception.Message);
    }

    [Theory]
    [InlineData("ServiceClientRegistry:Clients:0:Tenant:0")] // a misspelled Tenants would otherwise mean "no grants", silently
    [InlineData("ServiceClientRegistry:Clients:0:CredentialSecret")]
    public void Startup_WhenAnEntryCarriesAnUnknownField_Stops_WithoutNamingIt(string key)
    {
        var settings = Entry(0);
        settings[key] = SentinelValue;

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.Equal(
            "Configuration error: ServiceClientRegistry:Clients[0] an unknown field is not part of the contract.",
            exception.Message);
    }

    [Fact]
    public void Startup_WhenTenantsIsASingleValueInsteadOfAList_Stops_RatherThanReadingItAsNoTenants()
    {
        var settings = Entry(0);
        settings["ServiceClientRegistry:Clients:0:Tenants"] = "11111111-1111-4111-8111-111111111111";

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.Equal("Configuration error: ServiceClientRegistry:Clients[0] Tenants must be a list.", exception.Message);
    }

    [Theory]
    [InlineData("ServiceClientRegistry:Clients", "Configuration error: ServiceClientRegistry Clients must be a list.")]
    [InlineData("ServiceClientRegistry", "Configuration error: ServiceClientRegistry must be a section.")]
    public void Startup_WhenTheSectionOrItsListIsASingleValue_Stops_RatherThanReadingItAsEmpty(string key, string expected)
    {
        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(
            () => Register(new Dictionary<string, string?> { [key] = SentinelValue }));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void Startup_WhenKeysDifferOnlyInCase_ReadsThemAsConfigurationDoes()
    {
        // Configuration keys are case-insensitive (an environment variable may spell them in upper case).
        var tenant = Guid.NewGuid();
        var services = Register(new Dictionary<string, string?>
        {
            ["SERVICECLIENTREGISTRY:CLIENTS:0:CLIENTCODE"] = "registry-validation-upper",
            ["serviceclientregistry:clients:0:servicename"] = "Diten.MDM",
            ["ServiceClientRegistry:clients:0:allowedAudience"] = "TRUSTED_WORKFLOW_CONSUMER",
            ["ServiceClientRegistry:Clients:0:CREDENTIALHASH"] = RegistryStartup.NewHash(),
            ["ServiceClientRegistry:Clients:0:credentialversion"] = "v1",
            ["ServiceClientRegistry:Clients:0:TENANTS:0"] = tenant.ToString("D")
        });

        using var provider = services.BuildServiceProvider();
        var declared = Assert.Single(provider.GetRequiredService<ServiceClientRegistryDeclarations>().Clients);
        Assert.Equal("registry-validation-upper", declared.ClientCode);
        Assert.Equal([tenant], declared.Tenants);
    }

    [Fact]
    public void Startup_WhenTheSectionCarriesAnUnknownField_Stops()
    {
        var settings = Entry(0);
        settings["ServiceClientRegistry:Client:0:ClientCode"] = SentinelValue;

        var exception = Assert.Throws<ServiceClientRegistryConfigurationException>(() => Register(settings));

        Assert.DoesNotContain(SentinelValue, exception.ToString(), StringComparison.Ordinal);
    }

    private static IServiceCollection Register(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceClientRegistry(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services;
    }

    private static Dictionary<string, string?> Entry(
        int index, string clientCode = "registry-validation-client", string[]? tenants = null)
    {
        var prefix = $"ServiceClientRegistry:Clients:{index}:";
        var settings = new Dictionary<string, string?>
        {
            [prefix + "ClientCode"] = clientCode,
            [prefix + "ServiceName"] = "Diten.MDM",
            [prefix + "AllowedAudience"] = "TRUSTED_WORKFLOW_CONSUMER",
            [prefix + "CredentialHash"] = RegistryStartup.NewHash(),
            [prefix + "CredentialVersion"] = "v1"
        };
        for (var tenant = 0; tenant < (tenants?.Length ?? 0); tenant++)
            settings[prefix + $"Tenants:{tenant}"] = tenants![tenant];
        return settings;
    }
}
