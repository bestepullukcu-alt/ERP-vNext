using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Enums;

[assembly: InternalsVisibleTo("Diten.MdmService.Infrastructure")]

namespace Diten.MdmService.Domain.ValueObjects;

public sealed class ProductLegalEntityScopeVerifiedWriterAuthority
{
    public const string ForegroundReplacePurpose = "foreground-human-replace";
    public const string ForegroundReplacePermission = "mdm.product-legal-entity-scopes.replace";
    public const string ForegroundReplaceMutationKind = "ReplaceProductLegalEntityScopePolicyCommand";

    private const string ProofSchema = "product-legal-entity-scope-writer-authority/v1";

    private ProductLegalEntityScopeVerifiedWriterAuthority(
        Guid tenantId,
        Guid subjectId,
        Guid commandId,
        Guid aggregateId,
        string mutationKind,
        string payloadFingerprint)
    {
        TenantId = tenantId;
        SubjectId = subjectId;
        CommandId = commandId;
        AggregateType = AuditAggregateType.ProductLegalEntityScopePolicy;
        AggregateId = aggregateId;
        Purpose = ForegroundReplacePurpose;
        Operation = ProductAuditOperation.ProductLegalEntityScopePolicyReplaced;
        Permission = ForegroundReplacePermission;
        MutationKind = mutationKind;
        PayloadFingerprint = payloadFingerprint;
        ProofFingerprint = ComputeProofFingerprint(
            TenantId,
            SubjectId,
            CommandId,
            AggregateType,
            AggregateId,
            Purpose,
            Operation,
            Permission,
            MutationKind,
            PayloadFingerprint);
    }

    public Guid TenantId { get; }
    public Guid SubjectId { get; }
    public Guid CommandId { get; }
    public AuditAggregateType AggregateType { get; }
    public Guid AggregateId { get; }
    public string Purpose { get; }
    public ProductAuditOperation Operation { get; }
    public string Permission { get; }
    public string MutationKind { get; }
    public string PayloadFingerprint { get; }
    public string ProofFingerprint { get; }

    internal static ProductLegalEntityScopeVerifiedWriterAuthority IssueForegroundReplace(
        Guid tenantId,
        Guid subjectId,
        Guid commandId,
        Guid aggregateId,
        string mutationKind,
        string payloadFingerprint)
    {
        EnsureIdentity(tenantId, nameof(tenantId));
        EnsureIdentity(subjectId, nameof(subjectId));
        EnsureIdentity(commandId, nameof(commandId));
        EnsureIdentity(aggregateId, nameof(aggregateId));
        if (!string.Equals(
                mutationKind,
                ForegroundReplaceMutationKind,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Mutation kind is not the foreground Replace contract.", nameof(mutationKind));
        }
        if (!IsCanonicalSha256(payloadFingerprint))
        {
            throw new ArgumentException("Payload fingerprint is not canonical SHA-256.", nameof(payloadFingerprint));
        }

        return new ProductLegalEntityScopeVerifiedWriterAuthority(
            tenantId,
            subjectId,
            commandId,
            aggregateId,
            mutationKind,
            payloadFingerprint);
    }

    public bool MatchesForegroundReplace(
        Guid tenantId,
        Guid subjectId,
        Guid commandId,
        Guid aggregateId,
        string mutationKind,
        string payloadFingerprint)
    {
        if (tenantId == Guid.Empty
            || subjectId == Guid.Empty
            || commandId == Guid.Empty
            || aggregateId == Guid.Empty
            || !IsCanonicalSha256(payloadFingerprint)
            || TenantId != tenantId
            || SubjectId != subjectId
            || CommandId != commandId
            || AggregateType != AuditAggregateType.ProductLegalEntityScopePolicy
            || AggregateId != aggregateId
            || !string.Equals(Purpose, ForegroundReplacePurpose, StringComparison.Ordinal)
            || Operation != ProductAuditOperation.ProductLegalEntityScopePolicyReplaced
            || !string.Equals(Permission, ForegroundReplacePermission, StringComparison.Ordinal)
            || !string.Equals(MutationKind, ForegroundReplaceMutationKind, StringComparison.Ordinal)
            || !string.Equals(MutationKind, mutationKind, StringComparison.Ordinal)
            || !string.Equals(PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal)
            || !IsCanonicalSha256(ProofFingerprint))
        {
            return false;
        }

        var expected = ComputeProofFingerprint(
            TenantId,
            SubjectId,
            CommandId,
            AggregateType,
            AggregateId,
            Purpose,
            Operation,
            Permission,
            MutationKind,
            PayloadFingerprint);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected),
            Convert.FromHexString(ProofFingerprint));
    }

    private static string ComputeProofFingerprint(
        Guid tenantId,
        Guid subjectId,
        Guid commandId,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        string purpose,
        ProductAuditOperation operation,
        string permission,
        string mutationKind,
        string payloadFingerprint)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, ProofSchema);
        Append(hash, tenantId.ToString("D"));
        Append(hash, subjectId.ToString("D"));
        Append(hash, commandId.ToString("D"));
        Append(hash, ((int)aggregateType).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, aggregateId.ToString("D"));
        Append(hash, purpose);
        Append(hash, ((int)operation).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, permission);
        Append(hash, mutationKind);
        Append(hash, payloadFingerprint);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static void EnsureIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must be non-empty.", parameterName);
        }
    }
}
