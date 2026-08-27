using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Common.Tests.Authorization.Fu16;

public sealed class Fu16SchemeIsolationTests
{
    [Fact]
    public void Adding_fu16_does_not_replace_existing_hs256_default_scheme()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("Existing.HS256")
            .AddFu16DelegatedActor(options =>
            {
                options.TrustedIssuers.Add("https://auth.local");
                options.TrustedAudiences.Add("diten-ppm-service");
            });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal("Existing.HS256", options.DefaultScheme);
        Assert.NotEqual("Existing.HS256", Fu16AuthenticationOptions.Scheme);
    }

    [Fact]
    public async Task Default_composition_has_no_fu16_scheme_or_static_provider()
    {
        var services = new ServiceCollection();
        services.AddAuthentication("Existing.HS256");
        using var provider = services.BuildServiceProvider();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.Null(await schemes.GetSchemeAsync(Fu16AuthenticationOptions.Scheme));
        Assert.Null(provider.GetService<IFu16ValidationKeyProvider>());
        Assert.Null(provider.GetService<IFu16AuthorityDecisionClient>());
    }
}
