using Diten.BuildingBlocks.Security.Secrets;
using Diten.CrmService.Api.Security;
using Diten.CrmService.Application.Features.VisitQr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3E · W3E-BE-a (§2.2, "Anahtar yok") — the production key ring on <see cref="ISecretsProvider"/>: a configured
/// key (base64 ≥ 32 bytes) under <c>crm-visit-qr-signing</c> + <c>Crm:VisitQr:ActiveKeyId</c> is used; with none,
/// Development falls back to the deterministic dev key, any other environment has NO key (issue / print / a required
/// start then answer 503). The keys here are the test's own bytes — never a real secret.
/// </summary>
public sealed class VwW3EVisitQrKeyRingTests
{
    private static readonly byte[] TestKey = Enumerable.Range(7, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task A_configured_active_key_is_used_and_an_unusable_one_is_ignored()
    {
        var ring = Ring("Production", new Dictionary<string, string?>
        {
            ["Crm:VisitQr:ActiveKeyId"] = "k7",
            ["crm-visit-qr-signing:k7"] = Convert.ToBase64String(TestKey),
            ["crm-visit-qr-signing:short"] = Convert.ToBase64String(new byte[8]),
            ["crm-visit-qr-signing:bad"] = "not base64 !!"
        });
        var keys = await ring.GetAsync(default);
        Assert.True(keys.Available);
        Assert.Equal("k7", keys.ActiveKeyId);
        Assert.Equal(new[] { "k7" }, keys.Keys.Keys);
        Assert.Equal(TestKey, keys.Keys["k7"]);
    }

    [Fact]
    public async Task Development_without_a_key_uses_the_deterministic_development_key()
    {
        var keys = await Ring("Development", new Dictionary<string, string?>()).GetAsync(default);
        Assert.True(keys.Available);
        Assert.Equal(SecretsVisitQrKeyRing.DevelopmentKeyId, keys.ActiveKeyId);
        Assert.Equal(SecretsVisitQrKeyRing.DevelopmentKey(), keys.Keys["dev"]);
        Assert.Equal(SecretsVisitQrKeyRing.DevelopmentKey(), (await Ring("Development", new Dictionary<string, string?>()).GetAsync(default)).Keys["dev"]); // deterministic
    }

    [Fact]
    public void The_development_key_signs_a_known_vector_reproducible_outside_dotnet()
    {
        // HKDF-SHA256(SHA256("diten-crm-visit-qr-development-key-not-for-production"), salt "diten-crm-visit-qr-v1",
        // info = tenant "D") → HMAC-SHA256 over "DQR1.<account>.<version>.<keyId>"; recomputed independently (Node crypto)
        var keys = new VisitQrKeys("dev", new Dictionary<string, byte[]> { ["dev"] = SecretsVisitQrKeyRing.DevelopmentKey() });
        var payload = VisitQrSigner.Sign(keys, Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93"), Guid.Parse("daafb809-1b3a-404b-b7c5-fc0f8d9d147d"), 1);
        Assert.Equal("DQR1.Cbiv2jobS0C3xfwPjZ0UfQ.1.dev.MPeH6a3Uy9re0hQpYppsyywjrS-FJdE0-2iHAiXqF3w", payload);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Outside_development_without_a_key_nothing_is_available(string environment)
    {
        var keys = await Ring(environment, new Dictionary<string, string?> { ["Crm:VisitQr:ActiveKeyId"] = "k1" }).GetAsync(default);
        Assert.False(keys.Available);
        Assert.Empty(keys.Keys);
        Assert.Throws<InvalidOperationException>(() => VisitQrSigner.Sign(keys, Guid.NewGuid(), Guid.NewGuid(), 1));
    }

    [Fact]
    public void The_signing_key_is_in_no_configuration_file()
    {
        var api = Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src", "Diten.CrmService.Api");
        foreach (var file in Directory.GetFiles(api, "appsettings*.json"))
        {
            Assert.DoesNotContain("crm-visit-qr-signing", File.ReadAllText(file));
        }
    }

    private static SecretsVisitQrKeyRing Ring(string environment, IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var host = new FakeHost(environment);
        var secrets = new ConfigurationSecretsProvider(configuration, host,
            Microsoft.Extensions.Options.Options.Create(new SecretsProviderOptions { RequireEnvironmentVariablesInProduction = false }));
        return new SecretsVisitQrKeyRing(secrets, configuration, host, NullLogger<SecretsVisitQrKeyRing>.Instance);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.CrmService"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private sealed class FakeHost(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "CrmService";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
