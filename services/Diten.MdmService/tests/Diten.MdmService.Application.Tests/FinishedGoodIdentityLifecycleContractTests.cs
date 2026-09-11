using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodIdentityLifecycleContractTests
{
    [Fact]
    public void Admission_snapshot_canonicalizes_a_bounded_distinct_scope_and_rejects_invalid_input()
    {
        var tenantId = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var snapshot = CreateSnapshot(tenantId, legalEntityIds: [second, first]);

        Assert.Equal([first, second], snapshot.LegalEntityIds);
        Assert.Throws<InvalidOperationException>(() => CreateSnapshot(tenantId, legalEntityIds: [first, first]));
        Assert.Throws<InvalidOperationException>(() => CreateSnapshot(tenantId, legalEntityIds: [Guid.Empty]));
        Assert.Throws<InvalidOperationException>(() => CreateSnapshot(
            tenantId,
            legalEntityIds: Enumerable.Range(1, 201)
                .Select(value => Guid.Parse($"00000000-0000-0000-0000-{value:D12}"))
                .ToArray()));
    }

    [Fact]
    public void Admission_snapshot_rejects_unverified_scope_shapes_instead_of_manufacturing_a_fallback()
    {
        var tenantId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => FinishedGoodLifecycleAdmissionScopeSnapshot.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.UtcTicks,
            Guid.NewGuid(),
            1,
            ProductLegalEntityScopeMode.GroupWide,
            Guid.NewGuid(),
            1,
            ProductLegalEntityScopeRolloutMode.Enforced,
            [Guid.NewGuid()],
            new string('a', 64)));
    }

    [Fact]
    public void Enforced_groupwide_accepts_finite_human_admission_but_rejects_empty_admission()
    {
        var tenantId = Guid.NewGuid();
        var finishedGoodId = Guid.NewGuid();
        var gskuId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var observedAt = DateTimeOffset.UtcNow.UtcTicks;
        var policyId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var finiteAdmission = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var finiteException = Record.Exception(() => CreateSnapshot(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            actorId,
            observedAt,
            policyId,
            2,
            ProductLegalEntityScopeMode.GroupWide,
            rolloutId,
            3,
            ProductLegalEntityScopeRolloutMode.Enforced,
            finiteAdmission));
        var emptyException = Record.Exception(() => CreateSnapshot(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            actorId,
            observedAt,
            policyId,
            2,
            ProductLegalEntityScopeMode.GroupWide,
            rolloutId,
            3,
            ProductLegalEntityScopeRolloutMode.Enforced,
            []));

        Assert.Null(finiteException);
        Assert.IsType<InvalidOperationException>(emptyException);
    }

    [Fact]
    public void Admission_snapshot_fingerprint_binds_every_fact_and_exposed_scope_cannot_be_tampered()
    {
        var tenantId = Guid.NewGuid();
        var finishedGoodId = Guid.NewGuid();
        var gskuId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var observedAt = DateTimeOffset.UtcNow.UtcTicks;
        var policyId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var legalEntityIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var snapshot = CreateSnapshot(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            actorId,
            observedAt,
            policyId,
            2,
            ProductLegalEntityScopeMode.Scoped,
            rolloutId,
            3,
            ProductLegalEntityScopeRolloutMode.Enforced,
            legalEntityIds);
        var canonicalIds = snapshot.LegalEntityIds.ToArray();

        legalEntityIds[0] = Guid.NewGuid();
        var callerInputWasCopied = snapshot.LegalEntityIds.SequenceEqual(canonicalIds);
        if (snapshot.LegalEntityIds is Guid[] exposedArray)
        {
            exposedArray[0] = Guid.NewGuid();
        }
        else if (snapshot.LegalEntityIds is IList<Guid> exposedList && !exposedList.IsReadOnly)
        {
            exposedList[0] = Guid.NewGuid();
        }
        var exposedMutationWasDefended = snapshot.LegalEntityIds.SequenceEqual(canonicalIds);

        var pristine = CreateSnapshot(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            actorId,
            observedAt,
            policyId,
            2,
            ProductLegalEntityScopeMode.Scoped,
            rolloutId,
            3,
            ProductLegalEntityScopeRolloutMode.Enforced,
            canonicalIds);
        var staleFingerprintMutations = new[]
        {
            pristine with { TenantId = Guid.NewGuid() },
            pristine with { FinishedGoodId = Guid.NewGuid() },
            pristine with { GskuId = Guid.NewGuid() },
            pristine with { ProductDefinitionRevisionId = Guid.NewGuid() },
            pristine with { AdmissionCommandId = Guid.NewGuid() },
            pristine with { AdmissionActorSubjectId = Guid.NewGuid() },
            pristine with { AdmissionObservedAtUtcTicksV1 = pristine.AdmissionObservedAtUtcTicksV1 + 1 },
            pristine with { ScopePolicyId = Guid.NewGuid() },
            pristine with { ScopePolicyVersion = pristine.ScopePolicyVersion + 1 },
            pristine with { ScopeMode = ProductLegalEntityScopeMode.GroupWide },
            pristine with { RolloutStateId = Guid.NewGuid() },
            pristine with { RolloutVersion = pristine.RolloutVersion + 1 },
            pristine with { RolloutMode = ProductLegalEntityScopeRolloutMode.FailClosedSuspended },
            pristine with
            {
                LegalEntityIds = pristine.LegalEntityIds
                    .Skip(1)
                    .Append(Guid.NewGuid())
                    .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
                    .ToArray()
            }
        };
        var acceptedStaleFingerprints = staleFingerprintMutations.Count(IsValid);
        var malformed = pristine with { IntegrityFingerprint = "not-a-fingerprint" };
        var legacy = pristine with { SnapshotVersion = 0 };

        var malformedRejected = !IsValid(malformed);
        var legacyRejected = !IsValid(legacy);
        Assert.True(
            callerInputWasCopied
            && exposedMutationWasDefended
            && acceptedStaleFingerprints == 0
            && malformedRejected
            && legacyRejected,
            $"caller-copy={callerInputWasCopied}; exposed-copy={exposedMutationWasDefended}; " +
            $"accepted-stale={acceptedStaleFingerprints}; malformed-rejected={malformedRejected}; " +
            $"legacy-rejected={legacyRejected}");
    }

    [Fact]
    public void Workflow_checkpoint_values_keep_legacy_terminal_values_and_do_not_repurpose_them()
    {
        Assert.Equal(1, (int)FinishedGoodIdentityWorkflowCheckpoint.Prepared);
        Assert.Equal(9, (int)FinishedGoodIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(10, (int)FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        Assert.Equal(11, (int)FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired);
        Assert.Equal(12, (int)FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart);
        Assert.Equal(13, (int)FinishedGoodIdentityWorkflowCheckpoint.Superseded);
        Assert.Equal(
            Enum.GetValues<FinishedGoodIdentityWorkflowCheckpoint>().Length,
            Enum.GetValues<FinishedGoodIdentityWorkflowCheckpoint>().Distinct().Count());
    }

    [Fact]
    public void P1a_storage_contract_has_no_source_lifecycle_or_worker_authority_surface()
    {
        var operationProperties = typeof(Diten.MdmService.Domain.Entities.FinishedGoodIdentityWorkflowOperation)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        var repositoryMethods = typeof(Diten.MdmService.Domain.Repositories.IFinishedGoodIdentityWorkflowOperationRepository)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain("LifecycleStatus", operationProperties);
        Assert.DoesNotContain("ApplyDecision", repositoryMethods);
        Assert.DoesNotContain("StartWorkflow", repositoryMethods);
        Assert.DoesNotContain("CompleteWorkflow", repositoryMethods);
    }

    private static FinishedGoodLifecycleAdmissionScopeSnapshot CreateSnapshot(
        Guid tenantId,
        IReadOnlyList<Guid>? legalEntityIds = null)
    {
        var finishedGoodId = Guid.NewGuid();
        var gskuId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var observedAt = DateTimeOffset.UtcNow.UtcTicks;
        var policyId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        return CreateSnapshot(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            actorId,
            observedAt,
            policyId,
            1,
            ProductLegalEntityScopeMode.Scoped,
            rolloutId,
            1,
            ProductLegalEntityScopeRolloutMode.Enforced,
            legalEntityIds ?? [Guid.NewGuid()]);
    }

    private static FinishedGoodLifecycleAdmissionScopeSnapshot CreateSnapshot(
        Guid tenantId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid revisionId,
        Guid commandId,
        Guid actorId,
        long observedAt,
        Guid policyId,
        int policyVersion,
        ProductLegalEntityScopeMode scopeMode,
        Guid rolloutId,
        int rolloutVersion,
        ProductLegalEntityScopeRolloutMode rolloutMode,
        IReadOnlyList<Guid> legalEntityIds)
    {
        var canonicalIds = legalEntityIds.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
        return FinishedGoodLifecycleAdmissionScopeSnapshot.Create(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            actorId,
            observedAt,
            policyId,
            policyVersion,
            scopeMode,
            rolloutId,
            rolloutVersion,
            rolloutMode,
            canonicalIds,
            ComputeFingerprint(
                tenantId,
                finishedGoodId,
                gskuId,
                revisionId,
                commandId,
                actorId,
                observedAt,
                policyId,
                policyVersion,
                scopeMode,
                rolloutId,
                rolloutVersion,
                rolloutMode,
                canonicalIds));
    }

    private static string ComputeFingerprint(
        Guid tenantId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid revisionId,
        Guid commandId,
        Guid actorId,
        long observedAt,
        Guid policyId,
        int policyVersion,
        ProductLegalEntityScopeMode scopeMode,
        Guid rolloutId,
        int rolloutVersion,
        ProductLegalEntityScopeRolloutMode rolloutMode,
        IReadOnlyList<Guid> legalEntityIds)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "contract", "finished-good-lifecycle-admission-snapshot");
        Append(hash, "snapshot-version", FinishedGoodLifecycleAdmissionScopeSnapshot.CurrentSnapshotVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "tenant-id", tenantId.ToString("D"));
        Append(hash, "finished-good-id", finishedGoodId.ToString("D"));
        Append(hash, "gsku-id", gskuId.ToString("D"));
        Append(hash, "product-definition-revision-id", revisionId.ToString("D"));
        Append(hash, "admission-command-id", commandId.ToString("D"));
        Append(hash, "admission-actor-subject-id", actorId.ToString("D"));
        Append(hash, "admission-observed-at-utc-ticks-v1", observedAt.ToString(CultureInfo.InvariantCulture));
        Append(hash, "scope-policy-id", policyId.ToString("D"));
        Append(hash, "scope-policy-version", policyVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "scope-mode", ((int)scopeMode).ToString(CultureInfo.InvariantCulture));
        Append(hash, "rollout-state-id", rolloutId.ToString("D"));
        Append(hash, "rollout-version", rolloutVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "rollout-mode", ((int)rolloutMode).ToString(CultureInfo.InvariantCulture));
        Append(hash, "legal-entity-count", legalEntityIds.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < legalEntityIds.Count; index++)
        {
            Append(hash, $"legal-entity-id-{index}", legalEntityIds[index].ToString("D"));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string name, string value)
    {
        var encoded = Encoding.UTF8.GetBytes($"{name.Length}:{name}{value.Length}:{value}");
        hash.AppendData(encoded);
    }

    private static bool IsValid(FinishedGoodLifecycleAdmissionScopeSnapshot snapshot)
    {
        try
        {
            snapshot.EnsureValid();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
