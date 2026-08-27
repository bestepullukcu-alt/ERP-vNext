using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Common.Authorization.Fu16;

public sealed class Fu16AuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "Diten.FU16.DelegatedActor";
    public HashSet<string> TrustedIssuers { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TrustedAudiences { get; } = new(StringComparer.Ordinal);
    public TimeSpan ClockTolerance { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumProofLifetime { get; set; } = TimeSpan.FromMinutes(5);
}

public static class Fu16AuthenticationExtensions
{
    public static AuthenticationBuilder AddFu16DelegatedActor(
        this AuthenticationBuilder builder,
        Action<Fu16AuthenticationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IFu16TrustedRequestContextAccessor, HttpFu16TrustedRequestContextAccessor>();
        return builder.AddScheme<Fu16AuthenticationOptions, Fu16DelegatedActorAuthenticationHandler>(
            Fu16AuthenticationOptions.Scheme,
            configure);
    }
}
