using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Hosting;

namespace Diten.ApiGateway.Authentication;

/// <summary>
/// The gateway's required secrets — declared once here, validated at startup by Program.cs, and measured by
/// GatewaySecretRequirementsTests against this same list (not a copy).
/// </summary>
public static class GatewaySecretRequirements
{
    public const string CorrelationSigningKey = "Observability:Correlation:SigningKey";

    public static IReadOnlyList<RequiredSecretDefinition> For(IHostEnvironment environment) =>
    [
        new("JwtSettings:Secret", "ApiGateway", SecretRequirementKind.JwtCurrent),
        new("JwtSettings:PreviousSecrets", "ApiGateway", SecretRequirementKind.JwtPreviousCollection, Required: false),
        // Q151 D2 — without this key the edge adopts no inbound correlation, so every SupplyChain lifecycle step
        // after create (transitions, returns, claims) fails through the gateway. Required outside Development so a
        // misconfigured deploy fails at startup instead of silently (owner decision 2026-10-18); Development warns.
        new(CorrelationSigningKey, "ApiGateway", SecretRequirementKind.Generic,
            IsEnabled: () => !environment.IsDevelopment(), MinimumLength: 32)
    ];
}
