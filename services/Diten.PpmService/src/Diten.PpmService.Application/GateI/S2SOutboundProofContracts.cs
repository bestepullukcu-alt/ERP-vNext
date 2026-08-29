using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;

public sealed record S2STrustedRequestContext(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid DelegatedActorId,
    Guid DelegationId,
    Guid ServicePrincipalId,
    Guid CredentialId,
    string ClientId,
    string Issuer,
    string Audience,
    string TokenType,
    string ProtocolScope,
    string OperationId,
    IReadOnlyList<string> Permissions,
    string RequestHash,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public S2STrustedRequestContext Validate()
    {
        if (TenantId == Guid.Empty || EffectiveActorId == Guid.Empty || DelegatedActorId == Guid.Empty
            || DelegationId == Guid.Empty || ServicePrincipalId == Guid.Empty || CredentialId == Guid.Empty)
            throw new ArgumentException("S2S trusted identity is incomplete.");
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience) || string.IsNullOrWhiteSpace(TokenType)
            || string.IsNullOrWhiteSpace(ProtocolScope) || string.IsNullOrWhiteSpace(OperationId)
            || !S2SOutboundCanonicalRequestBinding.IsLowerHex64(RequestHash))
            throw new ArgumentException("S2S trusted strings are invalid.");
        if (Permissions.Count == 0 || Permissions.Any(string.IsNullOrWhiteSpace)
            || Permissions.Distinct(StringComparer.Ordinal).Count() != Permissions.Count)
            throw new ArgumentException("S2S permissions must be non-empty and ordinally unique.");
        if (TenantGrantVersion < 0 || ServicePrincipalVersion < 0 || CredentialGeneration < 0
            || IssuedAtUtc > NotBeforeUtc || NotBeforeUtc >= ExpiresAtUtc
            || ExpiresAtUtc - IssuedAtUtc > TimeSpan.FromMinutes(5))
            throw new ArgumentException("S2S freshness fields are invalid.");
        return this;
    }
}

public interface IS2STrustedRequestContextAccessor
{
    S2STrustedRequestContext? Current { get; }
    void Publish(S2STrustedRequestContext context);
}

public enum S2SOutboundProofDisposition
{
    Issued,
    Unauthenticated,
    Forbidden,
    Conflict,
    Unavailable
}

public sealed record S2SOutboundReceiverProfile(
    string OwnerModule,
    string Method,
    string Path,
    string Audience,
    string ClientId,
    string Operation,
    string Permission);

public static class S2SOutboundReceiverProfiles
{
    public static readonly S2SOutboundReceiverProfile DecisionRegistry = new(
        "MOD-0007", "POST", "/internal/v1/decision-registry/decision-references/validate",
        "diten-management-governance-service", "diten.management-governance",
        "decision-registry.decision-references.validate.v1",
        "management-governance.decision-references.validate");

    public static readonly S2SOutboundReceiverProfile Budgeting = new(
        "MOD-0136", "POST", "/internal/v1/fpa/budgeting/budget-version-references/validate",
        "diten-fpa-service", "diten.fpa",
        "budgeting.budget-version-references.validate",
        "budgeting.budget-version-references.validate");

    public static readonly S2SOutboundReceiverProfile ScenarioPlanning = new(
        "MOD-0138", "POST", "/internal/v1/fpa/scenario-planning/references/validate",
        "diten-fpa-service", "diten.fpa",
        "fpa.scenario-planning.references.validate",
        "fpa.scenario-planning.references.validate");

    public static readonly S2SOutboundReceiverProfile OutcomeTracking = new(
        "MOD-0072", "POST", "/internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate",
        "diten-decision-intelligence-service", "diten.decision-intelligence",
        "outcome-tracking.outcome-references.validate",
        "decision-intelligence.outcome-references.validate");

    public static IReadOnlyList<S2SOutboundReceiverProfile> All { get; } =
        [DecisionRegistry, Budgeting, ScenarioPlanning, OutcomeTracking];

    public static S2SOutboundReceiverProfile ForOwner(string ownerModule) =>
        All.Single(profile => string.Equals(profile.OwnerModule, ownerModule, StringComparison.Ordinal));
}

public sealed record S2SOutboundTrustedContext(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    bool DelegatedActorProofValidated);

public sealed record S2SOutboundProofRequest(
    S2SOutboundReceiverProfile Receiver,
    S2SOutboundTrustedContext TrustedContext,
    ReadOnlyMemory<byte> RawBody,
    string RequestHash);

public interface IS2SOutboundProof;

public sealed record S2SOutboundProofResult(
    S2SOutboundProofDisposition Disposition,
    IS2SOutboundProof? Proof = null,
    string? StableCode = null);

public interface IS2SOutboundProofProvider
{
    bool IsAvailable { get; }

    ValueTask<S2SOutboundProofResult> IssueAsync(
        S2SOutboundProofRequest request,
        CancellationToken cancellationToken);
}

public static class S2SOutboundCanonicalRequestBinding
{
    public static string Compute(
        string method,
        string absolutePath,
        ReadOnlySpan<byte> rawBody,
        Guid tenantId,
        string operation,
        IReadOnlyList<string> permissions)
    {
        if (string.IsNullOrEmpty(method)
            || !string.Equals(method, method.ToUpperInvariant(), StringComparison.Ordinal)
            || string.IsNullOrEmpty(absolutePath)
            || !absolutePath.StartsWith("/", StringComparison.Ordinal)
            || absolutePath.Contains("?", StringComparison.Ordinal)
            || tenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(operation)
            || permissions.Count == 0
            || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("S2S outbound request binding input is invalid.");
        }

        using var framed = new MemoryStream();
        Write(framed, Encoding.ASCII.GetBytes(method));
        Write(framed, Encoding.UTF8.GetBytes(absolutePath));
        Write(framed, rawBody);
        Write(framed, Encoding.ASCII.GetBytes(tenantId.ToString("D")));
        Write(framed, Encoding.UTF8.GetBytes(operation));
        foreach (var permission in permissions)
            Write(framed, Encoding.UTF8.GetBytes(permission));

        return Convert.ToHexString(SHA256.HashData(framed.ToArray())).ToLowerInvariant();
    }

    public static bool IsLowerHex64(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool FixedTimeMatches(string? supplied, string expected)
    {
        if (!IsLowerHex64(supplied) || !IsLowerHex64(expected))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied!),
            Encoding.ASCII.GetBytes(expected));
    }

    private static void Write(Stream target, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        target.Write(length);
        target.Write(value);
    }
}
