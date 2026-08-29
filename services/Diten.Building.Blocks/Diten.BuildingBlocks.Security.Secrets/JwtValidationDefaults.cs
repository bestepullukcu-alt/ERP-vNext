namespace Diten.BuildingBlocks.Security.Secrets;

/*
 * THE ONE CLOCK — BL-296 (2026-08-28).
 *
 * WHY THIS FILE EXISTS. Measured, not guessed: the same access token was judged by nine different inbound
 * validators, and they did not agree on when it expired. Seven allowed 30 seconds of clock skew; two allowed
 * none:
 *
 *     ClockSkew = TimeSpan.Zero            MdmService/Program.cs · DevEnablementService/Program.cs
 *                                          Platform/BackgroundJobs/PlatformActorHangfireAuthorizationFilter.cs
 *     ClockSkew = TimeSpan.FromSeconds(30) Platform/Infrastructure/DependencyInjection.cs
 *                                          AuthService/Infrastructure/DependencyInjection.cs
 *                                          HcmService/Program.cs · ApiGateway/GatewayJwtAuthenticationHandler.cs
 *                                          Diten.Web/Program.cs · Diten.Web/Filters/ShellAccessFilter.cs
 *
 * WHAT THE USER SAW. For thirty seconds after a token expired, Platform still said yes and MDM already said
 * no. One screen kept working while the next one said "yetkiniz yok". Hard to reproduce (the window is 30s
 * wide and moves with the token), and the cause is invisible from the symptom.
 *
 * WHY 30 SECONDS AND NOT ZERO — the value is the measurement's, not a preference:
 *   • Zero demands that five services, the gateway and the web shell agree on the wall clock to the
 *     millisecond. On one dev machine they do; behind NTP on separate hosts they do not, and the failure
 *     surfaces as a spurious 401 that nobody can reproduce. It also rejects a token whose `nbf` is a
 *     fraction of a second in the future at the moment it was issued.
 *   • The library's own default is 5 minutes. 30 seconds is already an order of magnitude stricter.
 *   • Access-token lifetime, measured: JwtSettings.AccessTokenExpirationMinutes = 15 (code default) and 120
 *     (AuthService appsettings.json). 30 seconds is 3.3% of the shorter one — a tolerance, not an extension.
 *   • Seven of the nine validators were already at 30 seconds. Standardising on Zero would have TIGHTENED
 *     the gateway, Platform, HCM and the web shell against real clock drift, to fix a defect that is about
 *     DISAGREEMENT, not about leniency.
 *
 * ⚠ THIS IS FOR INBOUND VALIDATION — `ValidateLifetime = true`, deciding whether to accept a request.
 * It is deliberately NOT applied where `ValidateLifetime = false` (AuthService's
 * `TokenService.GetPrincipalFromExpiredToken`, which decodes a KNOWN-expired token during refresh). There,
 * ClockSkew is never consulted by the library, so its value there says nothing and changing it changes
 * nothing. That, measured, is why AuthService appeared to carry both values at once: two different
 * operations, not two opinions.
 *
 * ⚠ THE GUARD. `JwtClockSkewGuardTests` (tests/architecture) fails the build if any production validator
 * writes a ClockSkew literal instead of referring to this constant. Do not paste a TimeSpan here — change
 * this line and every service moves together, which is the entire point.
 */
public static class JwtValidationDefaults
{
    /// <summary>
    /// The single tolerance every inbound JWT validator in the system applies to <c>exp</c> and <c>nbf</c>.
    /// See the file header for why the value is 30 seconds and why it must not be duplicated.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
}
