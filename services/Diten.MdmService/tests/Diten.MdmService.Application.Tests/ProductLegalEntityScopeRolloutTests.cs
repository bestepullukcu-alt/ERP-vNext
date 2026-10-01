using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeRolloutTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProductId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly ProductLegalEntityScopeEvaluator _evaluator = new();

    [Fact]
    public void Rollout_state_is_created_only_in_Preparation_with_expected_int_version_contract()
    {
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId,
            Guid.NewGuid(),
            ActorId,
            Now);

        Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, state.Mode);
        Assert.Equal(0, state.Version);
        Assert.IsAssignableFrom<IAuditIntentAggregate>(state);
        Assert.Empty(state.AuditIntents);
        Assert.Empty(state.AuditIntentReceipts);
        state.EnsureValid();
        state.EnsureExpectedVersion(0);
        Assert.Throws<InvalidOperationException>(() => state.EnsureExpectedVersion(1));
    }

    [Fact]
    public void H1b_writer_lease_has_exact_non_configurable_duration_and_rejects_invalid_generation_or_baseline()
    {
        Assert.Equal(120, ProductLegalEntityScopeWriterLease.DurationSeconds);
        var lease = new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = Guid.NewGuid(), ActorId = ActorId,
            MutationKind = "CreateGlobalProductDraft", PayloadFingerprint = new string('A', 64),
            Owner = "Diten.MDM:CreateGlobalProductDraft", AcquiredAtUtc = Now,
            ExpiresAtUtc = Now.AddSeconds(120), PreWriteStateHash = new string('B', 64)
        };
        lease.EnsureValid();
        Assert.True(lease.BaselineBound);
        lease.Generation = 0;
        Assert.Throws<InvalidOperationException>(lease.EnsureValid);
    }

    [Fact]
    public void H1b_activation_fence_requires_uppercase_hash_exact_action_reason_and_utc()
    {
        var fence = new ProductLegalEntityScopeActivationFence
        {
            Token = new string('A', 64), State = ProductLegalEntityScopeAdmissionState.Closing,
            Action = "ActivateEnforced", CommandId = Guid.NewGuid(), ActorId = ActorId,
            ReasonCode = "OWNER_APPROVED", AcquiredAtUtc = Now
        };
        fence.EnsureValid();
        fence.Token = new string('a', 64);
        Assert.Throws<InvalidOperationException>(fence.EnsureValid);
        Assert.False(ProductLegalEntityScopeActivationFence.Reason(" lower"));
    }

    [Fact]
    public void H1b_snapshot_requires_schema_one_lf_payload_utc_and_uppercase_hashes()
    {
        var payload = $"schemaVersion=1\nobservedUtcTicks={Now.UtcTicks}\n";
        var stablePayload = "schemaVersion=1\n";
        var snapshot = new ProductLegalEntityScopeInventorySnapshot
        {
            ObservedAtUtc = Now, CanonicalPayload = payload,
            StableFactsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stablePayload))),
            SnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
        };
        snapshot.EnsureValid();
        snapshot.CanonicalPayload = "schemaVersion=1";
        Assert.Throws<InvalidOperationException>(snapshot.EnsureValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Preparation_preserves_existing_tenant_only_behavior(bool existingTenantAccess)
    {
        var result = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Preparation,
            policy: null,
            existingTenantAccess,
            [],
            []));

        Assert.Equal(existingTenantAccess, result.Allowed);
        Assert.True(result.IsLegacyUnclassified);
        Assert.Equal(
            existingTenantAccess
                ? ProductLegalEntityScopeDecisionReason.PreparationExistingTenantAccessAllowed
                : ProductLegalEntityScopeDecisionReason.PreparationExistingTenantAccessDenied,
            result.Reason);
    }

    [Fact]
    public void Enforced_LegacyUnclassified_and_FailClosedSuspended_always_deny()
    {
        var legacy = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            policy: null,
            existingTenantAccess: true,
            [Guid.NewGuid()],
            [Guid.NewGuid()]));
        Assert.False(legacy.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.LegacyUnclassified, legacy.Reason);

        var suspended = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.FailClosedSuspended,
            CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []),
            existingTenantAccess: true,
            [Guid.NewGuid()],
            [Guid.NewGuid()]));
        Assert.False(suspended.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.FailClosedSuspended, suspended.Reason);
    }

    [Fact]
    public void Enforced_GroupWide_requires_nonempty_trusted_and_local_candidate_intersection()
    {
        var legalEntityId = Guid.NewGuid();
        var policy = CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []);
        var denied = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            policy,
            existingTenantAccess: true,
            [legalEntityId],
            []));
        Assert.False(denied.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.GroupWideCandidateEmpty, denied.Reason);

        var allowed = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            policy,
            existingTenantAccess: false,
            [legalEntityId],
            [legalEntityId]));
        Assert.True(allowed.Allowed);
        Assert.Equal([legalEntityId], allowed.MatchedLegalEntityIds);
        Assert.Equal(policy.Version, allowed.PolicyVersion);
    }

    [Fact]
    public void Enforced_Scoped_uses_full_multi_LE_intersection_without_first_value_truncation()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var second = Guid.Parse("20000000-0000-0000-0000-000000000000");
        var third = Guid.Parse("30000000-0000-0000-0000-000000000000");
        var policy = CreatePolicy(ProductLegalEntityScopeMode.Scoped, [second, third]);

        var result = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            policy,
            existingTenantAccess: true,
            [second, first],
            [first, second]));

        Assert.True(result.Allowed);
        Assert.Equal([first, second], result.EffectiveCandidateLegalEntityIds);
        Assert.Equal([second], result.MatchedLegalEntityIds);

        var denied = _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            policy,
            existingTenantAccess: true,
            [first],
            [first]));
        Assert.False(denied.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.ScopedCandidateNotMatched, denied.Reason);
    }

    [Fact]
    public void Ended_policy_has_no_current_period_and_denies_when_enforced()
    {
        var policy = CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []);
        policy.EndCurrent(0, Guid.NewGuid(), ActorId, Now.AddHours(1));

        var result = _evaluator.Evaluate(new ProductLegalEntityScopeEvaluationRequest(
            TenantId,
            ProductId,
            RolloutState(ProductLegalEntityScopeRolloutMode.Enforced),
            policy,
            Now.AddHours(1),
            true,
            [Guid.NewGuid()],
            [Guid.NewGuid()]));

        Assert.False(result.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.NoCurrentScopePeriod, result.Reason);
        Assert.False(result.IsLegacyUnclassified);
    }

    [Fact]
    public void Candidate_contract_rejects_duplicate_empty_and_over_200_without_truncation()
    {
        var duplicate = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []),
            true,
            [duplicate, duplicate],
            [duplicate])));
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []),
            true,
            [Guid.Empty],
            [])));
        Assert.Throws<ArgumentOutOfRangeException>(() => _evaluator.Evaluate(Request(
            ProductLegalEntityScopeRolloutMode.Enforced,
            CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []),
            true,
            Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray(),
            [])));
    }

    [Theory]
    [InlineData(ProductLegalEntityScopeRolloutMode.Preparation, true)]
    [InlineData(ProductLegalEntityScopeRolloutMode.Preparation, false)]
    [InlineData(ProductLegalEntityScopeRolloutMode.FailClosedSuspended, false)]
    public void Non_enforced_modes_do_not_parse_candidate_payloads(
        ProductLegalEntityScopeRolloutMode rolloutMode,
        bool expectedAllowed)
    {
        var malformed = Enumerable.Repeat(Guid.NewGuid(), 201).ToArray();
        var result = _evaluator.Evaluate(Request(
            rolloutMode,
            CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []),
            existingTenantAccess: expectedAllowed,
            malformed,
            [Guid.Empty]));

        Assert.Equal(expectedAllowed, result.Allowed);
        Assert.Empty(result.EffectiveCandidateLegalEntityIds);
    }

    [Fact]
    public void Foundation_budget_model_has_exact_non_overridable_limits()
    {
        Assert.Equal(200, ProductLegalEntityScopeSerializationBudget.Foundation.MaximumLegalEntityIdsPerSnapshot);
        Assert.Equal(100, ProductLegalEntityScopeSerializationBudget.Foundation.MaximumPeriodsPerPolicy);
        Assert.Equal(1_048_576, ProductLegalEntityScopeSerializationBudget.Foundation.MaximumSerializedBsonBytes);
    }

    [Theory]
    [InlineData(ProductLegalEntityScopeRolloutMode.Preparation)]
    [InlineData(ProductLegalEntityScopeRolloutMode.Enforced)]
    [InlineData(ProductLegalEntityScopeRolloutMode.FailClosedSuspended)]
    public void Cross_tenant_deleted_or_wrong_product_policy_evidence_denies_without_policy_facts(
        ProductLegalEntityScopeRolloutMode rolloutMode)
    {
        var legalEntityId = Guid.NewGuid();
        var crossTenant = CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []);
        crossTenant.TenantId = Guid.NewGuid();
        AssertInvalidEvidence(crossTenant, rolloutMode, legalEntityId);

        var wrongProduct = CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []);
        wrongProduct.GlobalProductId = Guid.NewGuid();
        AssertInvalidEvidence(wrongProduct, rolloutMode, legalEntityId);

        var deleted = CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []);
        deleted.IsDeleted = true;
        deleted.DeletedAt = Now;
        AssertInvalidEvidence(deleted, rolloutMode, legalEntityId);
    }

    [Fact]
    public void Cross_tenant_or_deleted_rollout_state_denies_before_evaluation()
    {
        var policy = CreatePolicy(ProductLegalEntityScopeMode.GroupWide, []);
        var state = RolloutState(ProductLegalEntityScopeRolloutMode.Preparation);
        state.TenantId = Guid.NewGuid();
        var request = new ProductLegalEntityScopeEvaluationRequest(
            TenantId,
            ProductId,
            state,
            policy,
            Now,
            true,
            [],
            []);

        var crossTenant = _evaluator.Evaluate(request);
        Assert.False(crossTenant.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence, crossTenant.Reason);
        Assert.Null(crossTenant.PolicyVersion);

        state.TenantId = TenantId;
        state.IsDeleted = true;
        var deleted = _evaluator.Evaluate(request);
        Assert.False(deleted.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence, deleted.Reason);
    }

    private static ProductLegalEntityScopeEvaluationRequest Request(
        ProductLegalEntityScopeRolloutMode rolloutMode,
        ProductLegalEntityScopePolicy? policy,
        bool existingTenantAccess,
        IReadOnlyCollection<Guid> trusted,
        IReadOnlyCollection<Guid> local) => new(
            TenantId,
            ProductId,
            RolloutState(rolloutMode),
            policy,
            Now,
            existingTenantAccess,
            trusted,
            local);

    private static ProductLegalEntityScopePolicy CreatePolicy(
        ProductLegalEntityScopeMode mode,
        IEnumerable<Guid> legalEntityIds) => ProductLegalEntityScopePolicy.Create(
            TenantId,
            ProductId,
            Guid.NewGuid(),
            mode,
            legalEntityIds,
            ActorId,
            Now);

    private static ProductLegalEntityScopeRolloutState RolloutState(
        ProductLegalEntityScopeRolloutMode mode)
    {
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId,
            Guid.NewGuid(),
            ActorId,
            Now);
        state.Mode = mode;
        return state;
    }

    private void AssertInvalidEvidence(
        ProductLegalEntityScopePolicy policy,
        ProductLegalEntityScopeRolloutMode rolloutMode,
        Guid legalEntityId)
    {
        var result = _evaluator.Evaluate(Request(
            rolloutMode,
            policy,
            existingTenantAccess: true,
            [legalEntityId],
            [legalEntityId]));

        Assert.False(result.Allowed);
        Assert.Equal(ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence, result.Reason);
        Assert.Null(result.PolicyVersion);
        Assert.Empty(result.EffectiveCandidateLegalEntityIds);
        Assert.Empty(result.MatchedLegalEntityIds);
    }
}
