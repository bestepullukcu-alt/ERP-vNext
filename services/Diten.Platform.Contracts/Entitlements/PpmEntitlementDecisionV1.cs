using System.Text.Json.Serialization;

namespace Diten.Platform.Contracts.Entitlements;

public static class PpmEntitlementDecisionContractV1
{
    public const string ContractName = "platform.ppm-entitlement-decision.v1";
    public const string ModuleCode = "PPM";
}

public sealed record PpmEntitlementDecisionV1(
    [property: JsonPropertyOrder(0)] Guid TenantId,
    [property: JsonPropertyOrder(1)] string ModuleCode,
    [property: JsonPropertyOrder(2)] bool IsAllowed,
    [property: JsonPropertyOrder(3)] DateTimeOffset ResolvedAtUtc,
    [property: JsonPropertyOrder(4)] DateTimeOffset? ExpiresAtUtc);
