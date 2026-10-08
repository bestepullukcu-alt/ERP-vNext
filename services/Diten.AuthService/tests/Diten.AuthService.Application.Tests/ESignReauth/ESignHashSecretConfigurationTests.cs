using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.ESignReauth;

/// <summary>
/// FU-0022-12 (CT decision 2026-10-08) — outside Development the ticket hash key comes from <c>ESign:HashSecret</c> only;
/// the <c>JwtSettings:Secret</c> fallback is a Development convenience. Without a key the service reports
/// <c>IsConfigured = false</c>, which the handlers turn into 503 <c>ESIGN_REAUTH_UNAVAILABLE</c> (fail closed).
/// </summary>
public sealed class ESignHashSecretConfigurationTests
{
    private const string JwtSecret = "jwt-secret-value-for-configuration-tests-0123456789";

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("AcceptanceHostPrep")]
    public void Outside_development_a_missing_hash_secret_does_not_fall_back_to_the_jwt_secret(string environmentName)
    {
        Assert.Null(ESignReauthTicketService.ResolveHashKey(hashSecret: null, JwtSecret, isDevelopment: false));
        Assert.Null(ESignReauthTicketService.ResolveHashKey(hashSecret: "  ", JwtSecret, isDevelopment: false));
        Assert.False(Service(hashSecret: "", environmentName).IsConfigured);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void A_configured_hash_secret_is_used_in_every_environment(string environmentName)
    {
        Assert.True(Service(hashSecret: "esign-hash-secret-0123456789", environmentName).IsConfigured);
        var key = ESignReauthTicketService.ResolveHashKey("esign-hash-secret-0123456789", JwtSecret, environmentName == "Development");
        Assert.NotNull(key);
        Assert.Equal(
            ESignReauthTicketService.ResolveHashKey("esign-hash-secret-0123456789", null, isDevelopment: false),
            key);
    }

    [Fact]
    public void Development_keeps_the_jwt_secret_fallback()
    {
        Assert.NotNull(ESignReauthTicketService.ResolveHashKey(null, JwtSecret, isDevelopment: true));
        Assert.True(Service(hashSecret: "", "Development").IsConfigured);
        Assert.Null(ESignReauthTicketService.ResolveHashKey(null, null, isDevelopment: true));
    }

    private static ESignReauthTicketService Service(string hashSecret, string environmentName) => new(
        tickets: null!, // the constructor only resolves the key; no repository or audit call happens here
        windows: null!,
        audit: null!,
        options: Options.Create(new ESignOptions { HashSecret = hashSecret }),
        jwtSettings: Options.Create(new JwtSettings { Secret = JwtSecret }),
        environment: new FixedEnvironment(environmentName));

    private sealed class FixedEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.AuthService.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
