using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling.Approval;

public sealed class PublishApprovalAuthorizationContractTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly Guid Model = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Version = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid Author = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Requester = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Publisher = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid DecisionActor = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private const string Hash = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(PublishApprovalRequirement.NotRequired)]
    [InlineData(PublishApprovalRequirement.Required)]
    public void Both_a04_branches_satisfy_contract_gates_but_publish_stays_non_executable(PublishApprovalRequirement requirement)
    {
        var result = PublishApprovalAuthorizationContract.Evaluate(Valid(requirement));
        Assert.Equal(503, result.HttpStatus);
        Assert.Equal(PublishApprovalFailureCodes.RuntimeUnavailable, result.StableCode);
        Assert.True(result.ContractGatesSatisfied);
        Assert.False(result.IsExecutable);
    }

    [Theory]
    [InlineData(AuthoritativeDecisionState.Denied, 403)]
    [InlineData(AuthoritativeDecisionState.Unavailable, 503)]
    [InlineData(AuthoritativeDecisionState.Malformed, 503)]
    [InlineData(AuthoritativeDecisionState.Indeterminate, 503)]
    public void Entitlement_permission_and_eligibility_use_typed_authoritative_states(AuthoritativeDecisionState state, int status)
    {
        foreach (var selector in Enumerable.Range(0, 3))
        {
            var gate = AuthorityPair(state);
            var input = selector switch { 0 => Valid() with { EntitlementDecision = gate }, 1 => Valid() with { PermissionDecision = gate }, _ => Valid() with { EligibilityDecision = gate } };
            var result = PublishApprovalAuthorizationContract.Evaluate(input);
            Assert.Equal(status, result.HttpStatus);
            Assert.False(result.ContractGatesSatisfied);
        }
    }

    [Fact]
    public void Authentication_and_visibility_are_independent_non_disclosing_gates()
    {
        Assert.Equal(401, PublishApprovalAuthorizationContract.Evaluate(Valid() with { Authenticated = false }).HttpStatus);
        Assert.Equal(404, PublishApprovalAuthorizationContract.Evaluate(Valid() with { TargetVisible = false }).HttpStatus);
    }

    [Theory]
    [InlineData(Fu16PublishActorProofState.Denied, 403)]
    [InlineData(Fu16PublishActorProofState.Unavailable, 503)]
    [InlineData(Fu16PublishActorProofState.Malformed, 503)]
    [InlineData(Fu16PublishActorProofState.Indeterminate, 503)]
    public void Fu16_proof_states_never_allow_fallback(Fu16PublishActorProofState state, int status)
    {
        var result = PublishApprovalAuthorizationContract.Evaluate(Valid() with { ActorProof = new(state, state == Fu16PublishActorProofState.Denied ? Publisher : null, false, false) });
        Assert.Equal(status, result.HttpStatus);
    }

    [Fact]
    public void Authority_version_fence_state_and_freshness_change_returns_409()
    {
        AssertConflict(Valid() with { EntitlementDecision = AuthorityPair(revalidatedVersion: 8) });
        AssertConflict(Valid() with { PermissionDecision = AuthorityPair(revalidatedFence: "fence-2") });
        AssertConflict(Valid() with { EligibilityDecision = AuthorityPair(revalidatedState: AuthoritativeDecisionState.Denied) });
        AssertConflict(Valid() with { EntitlementDecision = AuthorityPair(validUntil: Now) });
    }

    [Fact]
    public void Policy_request_version_requirement_and_freshness_change_returns_409()
    {
        var changed = new PublishApprovalPolicyRequestV1(Tenant, Model, Guid.NewGuid(), Hash, Publisher, Requester, Author);
        AssertConflict(Valid() with { RevalidatedRequest = changed });
        AssertConflict(Valid() with { RevalidatedDecision = Decision(PublishApprovalRequirement.NotRequired, 8) });
        AssertConflict(Valid() with { RevalidatedDecision = Decision(PublishApprovalRequirement.Required) });
        AssertConflict(Valid() with { RevalidatedDecision = new(PublishApprovalAuthorityState.Available, PublishApprovalRequirement.NotRequired, 7, Now.AddMinutes(-2), Now) });
    }

    [Theory]
    [InlineData(PublishApprovalAuthorityState.Unavailable)]
    [InlineData(PublishApprovalAuthorityState.Malformed)]
    [InlineData(PublishApprovalAuthorityState.Indeterminate)]
    public void Nonavailable_policy_authority_returns_503(PublishApprovalAuthorityState state)
    {
        var decision = new PublishApprovalPolicyDecisionV1(state, null, 7, Now.AddSeconds(-1), Now.AddMinutes(1));
        var result = PublishApprovalAuthorizationContract.Evaluate(Valid() with { InitialDecision = decision });
        Assert.Equal(503, result.HttpStatus);
        Assert.Equal(PublishApprovalFailureCodes.AuthorityUnavailable, result.StableCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Publisher_must_differ_from_requester_author_and_decision_actor(int collision)
    {
        var input = collision switch
        {
            0 => Valid() with { InitialRequest = Request(Publisher, Publisher, Author), RevalidatedRequest = Request(Publisher, Publisher, Author), Provenance = Provenance(requester: Publisher) },
            1 => Valid() with { InitialRequest = Request(Publisher, Requester, Publisher), RevalidatedRequest = Request(Publisher, Requester, Publisher), Provenance = Provenance(author: Publisher) },
            _ => Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = OutcomePair(decisionActor: Publisher) }
        };
        var result = PublishApprovalAuthorizationContract.Evaluate(input);
        Assert.Equal(403, result.HttpStatus);
        Assert.Equal(PublishApprovalFailureCodes.SodDenied, result.StableCode);
    }

    [Theory]
    [InlineData(ApprovalOutcomeProviderState.NotFound, 404)]
    [InlineData(ApprovalOutcomeProviderState.Unavailable, 503)]
    [InlineData(ApprovalOutcomeProviderState.Malformed, 503)]
    [InlineData(ApprovalOutcomeProviderState.Indeterminate, 503)]
    public void Required_outcome_provider_states_are_fail_closed(ApprovalOutcomeProviderState state, int status)
    {
        var pair = new ApprovalOutcomeRevalidationV1(new(state, null), new(state, null));
        var result = PublishApprovalAuthorizationContract.Evaluate(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = pair });
        Assert.Equal(status, result.HttpStatus);
    }

    [Theory]
    [InlineData(ApprovalOutcomeState.Denied)]
    [InlineData(ApprovalOutcomeState.NotFinal)]
    public void Required_outcome_must_be_final_approved(ApprovalOutcomeState state)
    {
        var result = PublishApprovalAuthorizationContract.Evaluate(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = OutcomePair(state) });
        Assert.Equal(403, result.HttpStatus);
    }

    [Fact]
    public void Outcome_binding_tuple_and_revalidation_are_exact()
    {
        AssertConflict(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = OutcomePair(modelId: Guid.NewGuid()) });
        AssertConflict(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = OutcomePair(revalidatedFence: "outcome-2") });
        AssertConflict(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = OutcomePair(validUntil: Now) });
        AssertConflict(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcome = OutcomePair(revalidatedState: ApprovalOutcomeState.Denied) });
    }

    [Fact]
    public void Required_branch_needs_reference_and_not_required_rejects_injected_outcome()
    {
        Assert.Equal(400, PublishApprovalAuthorizationContract.Evaluate(Valid(PublishApprovalRequirement.Required) with { ApprovalOutcomeReference = null }).HttpStatus);
        Assert.Equal(400, PublishApprovalAuthorizationContract.Evaluate(Valid() with { ApprovalOutcomeReference = Reference(), ApprovalOutcome = OutcomePair() }).HttpStatus);
    }

    private static void AssertConflict(PublishApprovalAuthorizationInput input)
    {
        var result = PublishApprovalAuthorizationContract.Evaluate(input);
        Assert.Equal(409, result.HttpStatus);
        Assert.Equal(PublishApprovalFailureCodes.AuthorityStale, result.StableCode);
    }

    private static PublishApprovalAuthorizationInput Valid(PublishApprovalRequirement requirement = PublishApprovalRequirement.NotRequired)
    {
        var provenance = Provenance(); var request = provenance.BindPolicyRequest(Publisher);
        return new(true, true, Now, provenance, request, Decision(requirement), request, Decision(requirement),
            new(Fu16PublishActorProofState.Available, Publisher, true, true), AuthorityPair(), AuthorityPair(), AuthorityPair(),
            requirement == PublishApprovalRequirement.Required ? Reference() : null,
            requirement == PublishApprovalRequirement.Required ? OutcomePair() : null);
    }

    private static PublishAuthorityRevalidationV1 AuthorityPair(AuthoritativeDecisionState state = AuthoritativeDecisionState.Allowed,
        long revalidatedVersion = 7, string revalidatedFence = "fence-1", AuthoritativeDecisionState? revalidatedState = null, DateTime? validUntil = null) =>
        new(new(state, 7, "fence-1", Now.AddSeconds(-1), validUntil ?? Now.AddMinutes(1)), new(revalidatedState ?? state, revalidatedVersion, revalidatedFence, Now.AddSeconds(-1), validUntil ?? Now.AddMinutes(1)));

    private static ApprovalOutcomeRevalidationV1 OutcomePair(ApprovalOutcomeState state = ApprovalOutcomeState.FinalApproved,
        Guid? decisionActor = null, Guid? modelId = null, string revalidatedFence = "outcome-1", ApprovalOutcomeState? revalidatedState = null, DateTime? validUntil = null) =>
        new(new(ApprovalOutcomeProviderState.Available, Outcome(state, decisionActor, modelId, "outcome-1", validUntil)), new(ApprovalOutcomeProviderState.Available, Outcome(revalidatedState ?? state, decisionActor, modelId, revalidatedFence, validUntil)));

    private static ApprovalOutcomeBindingV1 Outcome(ApprovalOutcomeState state, Guid? decisionActor, Guid? modelId, string fence, DateTime? validUntil) =>
        new(Tenant, modelId ?? Model, Version, Hash, decisionActor ?? DecisionActor, state, fence, Now.AddSeconds(-1), validUntil ?? Now.AddMinutes(1));
    private static PublishActorProvenanceV1 Provenance(Guid? author = null, Guid? requester = null) => new(Tenant, Model, Version, Hash, author ?? Author, requester ?? Requester, Now.AddMinutes(-1));
    private static PublishApprovalPolicyRequestV1 Request(Guid publisher, Guid requester, Guid author) => new(Tenant, Model, Version, Hash, publisher, requester, author);
    private static PublishApprovalPolicyDecisionV1 Decision(PublishApprovalRequirement requirement, long version = 7) => new(PublishApprovalAuthorityState.Available, requirement, version, Now.AddSeconds(-1), Now.AddMinutes(1));
    private static ApprovalOutcomeReferenceV1 Reference() => new(ApprovalOutcomeReferenceV1.ExpectedContractName, ApprovalOutcomeReferenceV1.ExpectedContractVersion, Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
}
