using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;

public enum Fu16OutboundProofDisposition
{
    Issued,
    Unauthenticated,
    Forbidden,
    Conflict,
    Unavailable
}

public sealed record Fu16OutboundReceiverProfile(
    string OwnerModule,
    string Method,
    string Path,
    string Audience,
    string ClientId,
    string Operation,
    string Permission);

public static class Fu16OutboundReceiverProfiles
{
    public static readonly Fu16OutboundReceiverProfile DecisionRegistry = new(
        "MOD-0007", "POST", "/internal/v1/decision-registry/decision-references/validate",
        "diten-management-governance-service", "diten.management-governance",
        "decision-registry.decision-references.validate.v1",
        "management-governance.decision-references.validate");

    public static readonly Fu16OutboundReceiverProfile Budgeting = new(
        "MOD-0136", "POST", "/internal/v1/fpa/budgeting/budget-version-references/validate",
        "diten-fpa-service", "diten.fpa",
        "budgeting.budget-version-references.validate",
        "budgeting.budget-version-references.validate");

    public static readonly Fu16OutboundReceiverProfile ScenarioPlanning = new(
        "MOD-0138", "POST", "/internal/v1/fpa/scenario-planning/references/validate",
        "diten-fpa-service", "diten.fpa",
        "fpa.scenario-planning.references.validate",
        "fpa.scenario-planning.references.validate");

    public static readonly Fu16OutboundReceiverProfile OutcomeTracking = new(
        "MOD-0072", "POST", "/internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate",
        "diten-decision-intelligence-service", "diten.decision-intelligence",
        "outcome-tracking.outcome-references.validate",
        "decision-intelligence.outcome-references.validate");

    public static IReadOnlyList<Fu16OutboundReceiverProfile> All { get; } =
        [DecisionRegistry, Budgeting, ScenarioPlanning, OutcomeTracking];

    public static Fu16OutboundReceiverProfile ForOwner(string ownerModule) =>
        All.Single(profile => string.Equals(profile.OwnerModule, ownerModule, StringComparison.Ordinal));
}

public sealed record Fu16OutboundTrustedContext(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    bool DelegatedActorProofValidated);

public sealed record Fu16OutboundProofRequest(
    Fu16OutboundReceiverProfile Receiver,
    Fu16OutboundTrustedContext TrustedContext,
    ReadOnlyMemory<byte> RawBody,
    string RequestHash);

public interface IFu16OutboundProof;

public sealed record Fu16OutboundProofResult(
    Fu16OutboundProofDisposition Disposition,
    IFu16OutboundProof? Proof = null,
    string? StableCode = null);

public interface IFu16OutboundProofProvider
{
    bool IsAvailable { get; }

    ValueTask<Fu16OutboundProofResult> IssueAsync(
        Fu16OutboundProofRequest request,
        CancellationToken cancellationToken);
}

public static class Fu16OutboundCanonicalRequestBinding
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
            throw new ArgumentException("FU16 outbound request binding input is invalid.");
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
