using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.ValueObjects;

public sealed record FinishedGoodLifecycleAdmissionScopeSnapshot
{
    private IReadOnlyList<Guid> _legalEntityIds = Array.AsReadOnly(Array.Empty<Guid>());

    public int SnapshotVersion { get; init; }
    public Guid TenantId { get; init; }
    public Guid FinishedGoodId { get; init; }
    public Guid GskuId { get; init; }
    public Guid ProductDefinitionRevisionId { get; init; }
    public Guid AdmissionCommandId { get; init; }
    public Guid AdmissionActorSubjectId { get; init; }
    public long AdmissionObservedAtUtcTicksV1 { get; init; }
    public Guid ScopePolicyId { get; init; }
    public int ScopePolicyVersion { get; init; }
    public ProductLegalEntityScopeMode ScopeMode { get; init; }
    public Guid RolloutStateId { get; init; }
    public int RolloutVersion { get; init; }
    public ProductLegalEntityScopeRolloutMode RolloutMode { get; init; }
    public IReadOnlyList<Guid> LegalEntityIds
    {
        get => _legalEntityIds;
        init => _legalEntityIds = Array.AsReadOnly((value ?? Array.Empty<Guid>()).ToArray());
    }

    public string IntegrityFingerprint { get; init; } = string.Empty;

    public const int CurrentSnapshotVersion = 1;
    public const int MaximumLegalEntityIds = 200;

    public static FinishedGoodLifecycleAdmissionScopeSnapshot Create(
        Guid tenantId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid productDefinitionRevisionId,
        Guid admissionCommandId,
        Guid admissionActorSubjectId,
        long admissionObservedAtUtcTicksV1,
        Guid scopePolicyId,
        int scopePolicyVersion,
        ProductLegalEntityScopeMode scopeMode,
        Guid rolloutStateId,
        int rolloutVersion,
        ProductLegalEntityScopeRolloutMode rolloutMode,
        IEnumerable<Guid> legalEntityIds)
    {
        ArgumentNullException.ThrowIfNull(legalEntityIds);
        var snapshot = new FinishedGoodLifecycleAdmissionScopeSnapshot
        {
            SnapshotVersion = CurrentSnapshotVersion,
            TenantId = tenantId,
            FinishedGoodId = finishedGoodId,
            GskuId = gskuId,
            ProductDefinitionRevisionId = productDefinitionRevisionId,
            AdmissionCommandId = admissionCommandId,
            AdmissionActorSubjectId = admissionActorSubjectId,
            AdmissionObservedAtUtcTicksV1 = admissionObservedAtUtcTicksV1,
            ScopePolicyId = scopePolicyId,
            ScopePolicyVersion = scopePolicyVersion,
            ScopeMode = scopeMode,
            RolloutStateId = rolloutStateId,
            RolloutVersion = rolloutVersion,
            RolloutMode = rolloutMode,
            LegalEntityIds = legalEntityIds.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray()
        };
        snapshot = snapshot with { IntegrityFingerprint = snapshot.ComputeIntegrityFingerprint() };
        snapshot.EnsureValid();
        return snapshot;
    }

    public static FinishedGoodLifecycleAdmissionScopeSnapshot Create(
        Guid tenantId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid productDefinitionRevisionId,
        Guid admissionCommandId,
        Guid admissionActorSubjectId,
        long admissionObservedAtUtcTicksV1,
        Guid scopePolicyId,
        int scopePolicyVersion,
        ProductLegalEntityScopeMode scopeMode,
        Guid rolloutStateId,
        int rolloutVersion,
        ProductLegalEntityScopeRolloutMode rolloutMode,
        IEnumerable<Guid> legalEntityIds,
        string integrityFingerprint)
    {
        var snapshot = Create(
            tenantId,
            finishedGoodId,
            gskuId,
            productDefinitionRevisionId,
            admissionCommandId,
            admissionActorSubjectId,
            admissionObservedAtUtcTicksV1,
            scopePolicyId,
            scopePolicyVersion,
            scopeMode,
            rolloutStateId,
            rolloutVersion,
            rolloutMode,
            legalEntityIds);

        if (!string.Equals(integrityFingerprint, snapshot.IntegrityFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("FINISHED_GOOD_ADMISSION_SCOPE_SNAPSHOT_FINGERPRINT_INVALID");
        }

        return snapshot;
    }

    public void EnsureValid()
    {
        if (SnapshotVersion != CurrentSnapshotVersion
            || TenantId == Guid.Empty
            || FinishedGoodId == Guid.Empty
            || GskuId == Guid.Empty
            || ProductDefinitionRevisionId == Guid.Empty
            || AdmissionCommandId == Guid.Empty
            || AdmissionActorSubjectId == Guid.Empty
            || AdmissionObservedAtUtcTicksV1 <= 0
            || ScopePolicyId == Guid.Empty
            || ScopePolicyVersion < 0
            || !Enum.IsDefined(ScopeMode)
            || RolloutStateId == Guid.Empty
            || RolloutVersion < 0
            || !Enum.IsDefined(RolloutMode)
            || LegalEntityIds is null
            || LegalEntityIds.Count > MaximumLegalEntityIds
            || LegalEntityIds.Any(id => id == Guid.Empty)
            || LegalEntityIds.Distinct().Count() != LegalEntityIds.Count
            || !LegalEntityIds.SequenceEqual(
                LegalEntityIds.OrderBy(id => id.ToString("D"), StringComparer.Ordinal))
            || string.IsNullOrWhiteSpace(IntegrityFingerprint)
            || IntegrityFingerprint.Length != 64
            || !IntegrityFingerprint.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new InvalidOperationException("FINISHED_GOOD_ADMISSION_SCOPE_SNAPSHOT_INVALID");
        }

        if ((ScopeMode == ProductLegalEntityScopeMode.Scoped && LegalEntityIds.Count == 0)
            || (ScopeMode == ProductLegalEntityScopeMode.GroupWide
                && RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced
                && LegalEntityIds.Count == 0))
        {
            throw new InvalidOperationException("FINISHED_GOOD_ADMISSION_SCOPE_SNAPSHOT_SCOPE_INVALID");
        }

        if (!string.Equals(IntegrityFingerprint, ComputeIntegrityFingerprint(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("FINISHED_GOOD_ADMISSION_SCOPE_SNAPSHOT_FINGERPRINT_INVALID");
        }
    }

    private string ComputeIntegrityFingerprint()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "contract", "finished-good-lifecycle-admission-snapshot");
        Append(hash, "snapshot-version", SnapshotVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "tenant-id", TenantId.ToString("D"));
        Append(hash, "finished-good-id", FinishedGoodId.ToString("D"));
        Append(hash, "gsku-id", GskuId.ToString("D"));
        Append(hash, "product-definition-revision-id", ProductDefinitionRevisionId.ToString("D"));
        Append(hash, "admission-command-id", AdmissionCommandId.ToString("D"));
        Append(hash, "admission-actor-subject-id", AdmissionActorSubjectId.ToString("D"));
        Append(hash, "admission-observed-at-utc-ticks-v1", AdmissionObservedAtUtcTicksV1.ToString(CultureInfo.InvariantCulture));
        Append(hash, "scope-policy-id", ScopePolicyId.ToString("D"));
        Append(hash, "scope-policy-version", ScopePolicyVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "scope-mode", ((int)ScopeMode).ToString(CultureInfo.InvariantCulture));
        Append(hash, "rollout-state-id", RolloutStateId.ToString("D"));
        Append(hash, "rollout-version", RolloutVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "rollout-mode", ((int)RolloutMode).ToString(CultureInfo.InvariantCulture));
        Append(hash, "legal-entity-count", LegalEntityIds.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < LegalEntityIds.Count; index++)
        {
            Append(hash, $"legal-entity-id-{index}", LegalEntityIds[index].ToString("D"));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string name, string value)
    {
        var encoded = Encoding.UTF8.GetBytes($"{name.Length}:{name}{value.Length}:{value}");
        hash.AppendData(encoded);
    }
}
