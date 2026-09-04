using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.Approval;

public sealed class ApprovalValueContractTests
{
    private static readonly Guid TenantId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly Guid ModelId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid AuthorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RequesterId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private const string Hash = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Provenance_has_exact_eight_fields_and_binds_policy_tuple()
    {
        var provenance = Provenance();
        var properties = typeof(PublishActorProvenanceV1).GetProperties().Select(x => x.Name).Order().ToArray();
        Assert.Equal(new[] { "CapturedAtUtc", "ContentHash", "ModelAuthorActorId", "ProcessModelId",
            "ProcessModelVersionId", "ProvenanceVersion", "PublishRequesterActorId", "TenantId" }, properties);

        var request = provenance.BindPolicyRequest(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        Assert.Equal(provenance.TenantId, request.TenantId);
        Assert.Equal(provenance.ProcessModelId, request.ModelId);
        Assert.Equal(provenance.ProcessModelVersionId, request.VersionId);
        Assert.Equal(provenance.ModelAuthorActorId, request.AuthorActorId);
        Assert.Equal(provenance.PublishRequesterActorId, request.RequesterActorId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Empty_provenance_identity_is_rejected(int position)
    {
        var values = new[] { TenantId, ModelId, VersionId, AuthorId, RequesterId };
        values[position] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => new PublishActorProvenanceV1(
            values[0], values[1], values[2], Hash, values[3], values[4], Now));
    }

    [Fact]
    public void Provenance_requires_exact_hash_utc_and_version()
    {
        Assert.Throws<ArgumentException>(() => new PublishActorProvenanceV1(TenantId, ModelId, VersionId,
            "sha256:ABC", AuthorId, RequesterId, Now));
        Assert.Throws<ArgumentException>(() => new PublishActorProvenanceV1(TenantId, ModelId, VersionId,
            Hash, AuthorId, RequesterId, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => new PublishActorProvenanceV1(TenantId, ModelId, VersionId,
            Hash, AuthorId, RequesterId, Now, "2.0"));
    }

    [Fact]
    public void Policy_request_has_exact_seven_fields()
    {
        var properties = typeof(PublishApprovalPolicyRequestV1).GetProperties().Select(x => x.Name).Order().ToArray();
        Assert.Equal(new[] { "AuthorActorId", "ContentHash", "ModelId", "PublisherActorId",
            "RequesterActorId", "TenantId", "VersionId" }, properties);
    }

    [Fact]
    public void Policy_decision_enforces_state_requirement_version_and_freshness()
    {
        var decision = Available(PublishApprovalRequirement.NotRequired);
        Assert.True(decision.IsFreshAt(Now));
        Assert.False(decision.IsFreshAt(Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => new PublishApprovalPolicyDecisionV1(
            PublishApprovalAuthorityState.Available, null, 1, Now, Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => new PublishApprovalPolicyDecisionV1(
            PublishApprovalAuthorityState.Unavailable, PublishApprovalRequirement.Required, 1, Now, Now.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PublishApprovalPolicyDecisionV1(
            PublishApprovalAuthorityState.Available, PublishApprovalRequirement.Required, 0, Now, Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => new PublishApprovalPolicyDecisionV1(
            PublishApprovalAuthorityState.Available, PublishApprovalRequirement.Required, 1, Now, Now));
    }

    [Fact]
    public void Approval_reference_round_trips_exact_three_fields()
    {
        const string json = "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"1.0\",\"ApprovalOutcomeId\":\"01234567-89ab-cdef-0123-456789abcdef\"}";
        var reference = ApprovalOutcomeReferenceV1.ParseExact(json);
        Assert.Equal(json, reference.ToExactJson());
        Assert.Equal(3, typeof(ApprovalOutcomeReferenceV1).GetProperties().Length);
    }

    public static IEnumerable<object[]> InvalidReferences => new[]
    {
        "{}",
        "[]",
        "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"1.0\"}",
        "{\"ContractName\":\"wrong\",\"ContractVersion\":\"1.0\",\"ApprovalOutcomeId\":\"01234567-89ab-cdef-0123-456789abcdef\"}",
        "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"2.0\",\"ApprovalOutcomeId\":\"01234567-89ab-cdef-0123-456789abcdef\"}",
        "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"1.0\",\"ApprovalOutcomeId\":\"01234567-89AB-CDEF-0123-456789ABCDEF\"}",
        "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"1.0\",\"ApprovalOutcomeId\":\"00000000-0000-0000-0000-000000000000\"}",
        "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"1.0\",\"ApprovalOutcomeId\":\"01234567-89ab-cdef-0123-456789abcdef\"}",
        "{\"ContractName\":\"platform.approval-outcome-reference\",\"ContractVersion\":\"1.0\",\"ApprovalOutcomeId\":\"01234567-89ab-cdef-0123-456789abcdef\",\"ApprovalOutcomeVersion\":1}"
    }.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(InvalidReferences))]
    public void Approval_reference_rejects_missing_duplicate_unknown_and_noncanonical_fields(string json) =>
        Assert.ThrowsAny<Exception>(() => ApprovalOutcomeReferenceV1.ParseExact(json));

    [Fact]
    public async Task Absent_upstream_boundaries_are_test_only_default_unavailable_and_cancellation_propagates()
    {
        var request = Provenance().BindPolicyRequest(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var result = await new UnavailableProofBoundary().ValidateAsync(request, CancellationToken.None);
        Assert.Equal(Fu16PublishActorProofState.Unavailable, result.State);
        Assert.Null(result.EffectiveActorId);

        var policy = await new UnavailablePolicyProvider().ResolveAsync(request, CancellationToken.None);
        Assert.Equal(PublishApprovalAuthorityState.Unavailable, policy.AuthorityState);

        var reference = new ApprovalOutcomeReferenceV1(ApprovalOutcomeReferenceV1.ExpectedContractName,
            ApprovalOutcomeReferenceV1.ExpectedContractVersion, Guid.NewGuid());
        var outcome = await new UnavailableOutcomeProvider().ResolveAsync(reference, request, CancellationToken.None);
        Assert.Equal(ApprovalOutcomeProviderState.Unavailable, outcome.State);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new UnavailableProofBoundary().ValidateAsync(request, cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new UnavailablePolicyProvider().ResolveAsync(request, cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new UnavailableOutcomeProvider().ResolveAsync(reference, request, cancellation.Token));
    }

    private static PublishActorProvenanceV1 Provenance() =>
        new(TenantId, ModelId, VersionId, Hash, AuthorId, RequesterId, Now);

    private static PublishApprovalPolicyDecisionV1 Available(PublishApprovalRequirement requirement) =>
        new(PublishApprovalAuthorityState.Available, requirement, 7, Now.AddSeconds(-1), Now.AddMinutes(1));

    private sealed class UnavailableProofBoundary : IFu16PublishActorProofBoundary
    {
        public ValueTask<Fu16PublishActorProofResult> ValidateAsync(PublishApprovalPolicyRequestV1 request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Fu16PublishActorProofResult.Unavailable()); }
    }

    private sealed class UnavailablePolicyProvider : IPublishApprovalPolicyDecisionProvider
    {
        public ValueTask<PublishApprovalPolicyDecisionV1> ResolveAsync(PublishApprovalPolicyRequestV1 request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Unavailable()); }
        public ValueTask<PublishApprovalPolicyDecisionV1> RevalidateAsync(PublishApprovalPolicyRequestV1 request, long expectedPolicyVersion, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Unavailable()); }
        private static PublishApprovalPolicyDecisionV1 Unavailable() => new(PublishApprovalAuthorityState.Unavailable, null, 1, Now, Now.AddSeconds(1));
    }

    private sealed class UnavailableOutcomeProvider : IApprovalOutcomeDecisionProvider
    {
        public ValueTask<ApprovalOutcomeProviderResult> ResolveAsync(ApprovalOutcomeReferenceV1 reference, PublishApprovalPolicyRequestV1 request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ApprovalOutcomeProviderResult.Unavailable()); }
        public ValueTask<ApprovalOutcomeProviderResult> RevalidateAsync(ApprovalOutcomeReferenceV1 reference, PublishApprovalPolicyRequestV1 request, string expectedFence, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ApprovalOutcomeProviderResult.Unavailable()); }
    }
}
