using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;

namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;

public static class PublishApprovalFailureCodes
{
    public const string InvalidRequest = "process_model_publish_approval_invalid_request";
    public const string AuthenticationRequired = "process_model_publish_authentication_required";
    public const string PermissionDenied = "process_model_publish_permission_denied";
    public const string SodDenied = "process_model_publish_sod_denied";
    public const string ResourceNotFound = "process_model_publish_resource_not_found";
    public const string AuthorityStale = "process_model_publish_authority_stale";
    public const string AuthorityUnavailable = "process_model_publish_authority_unavailable";
    public const string Fu16OnboardingUnavailable = "process_model_publish_fu16_onboarding_unavailable";
    public const string RuntimeUnavailable = "process_model_publish_runtime_unavailable";
}

public enum AuthoritativeDecisionState { Allowed, Denied, Unavailable, Malformed, Indeterminate }

public sealed record PublishAuthorityDecisionV1
{
    public PublishAuthorityDecisionV1(AuthoritativeDecisionState state, long version, string fence,
        DateTime observedAtUtc, DateTime validUntilUtc)
    {
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (string.IsNullOrEmpty(fence) || fence.Length > 256 || !string.Equals(fence, fence.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("invalid_authority_fence", nameof(fence));
        ObservedAtUtc = RequireUtc(observedAtUtc, nameof(observedAtUtc));
        ValidUntilUtc = RequireUtc(validUntilUtc, nameof(validUntilUtc));
        if (validUntilUtc <= observedAtUtc) throw new ArgumentException("invalid_authority_interval", nameof(validUntilUtc));
        State = state; Version = version; Fence = fence;
    }
    public AuthoritativeDecisionState State { get; }
    public long Version { get; }
    public string Fence { get; }
    public DateTime ObservedAtUtc { get; }
    public DateTime ValidUntilUtc { get; }
    public bool IsFreshAt(DateTime nowUtc) { nowUtc=RequireUtc(nowUtc,nameof(nowUtc));return nowUtc>=ObservedAtUtc&&nowUtc<ValidUntilUtc; }
    internal static DateTime RequireUtc(DateTime value,string name)=>value.Kind!=DateTimeKind.Utc?throw new ArgumentException("utc_required",name):value;
}

public sealed record PublishAuthorityRevalidationV1(PublishAuthorityDecisionV1 Initial, PublishAuthorityDecisionV1 Revalidated);

public enum Fu16PublishActorProofState { Available, Denied, Unavailable, Malformed, Indeterminate }
public sealed record Fu16PublishActorProofResult(Fu16PublishActorProofState State,Guid? EffectiveActorId,bool Fresh,bool RequestBound)
{ public static Fu16PublishActorProofResult Unavailable()=>new(Fu16PublishActorProofState.Unavailable,null,false,false); }
public interface IFu16PublishActorProofBoundary
{ ValueTask<Fu16PublishActorProofResult> ValidateAsync(PublishApprovalPolicyRequestV1 request,CancellationToken cancellationToken); }

public interface IPublishApprovalPolicyDecisionProvider
{
    ValueTask<PublishApprovalPolicyDecisionV1> ResolveAsync(PublishApprovalPolicyRequestV1 request,CancellationToken cancellationToken);
    ValueTask<PublishApprovalPolicyDecisionV1> RevalidateAsync(PublishApprovalPolicyRequestV1 request,long expectedPolicyVersion,CancellationToken cancellationToken);
}

public enum ApprovalOutcomeProviderState { Available, NotFound, Unavailable, Malformed, Indeterminate }
public enum ApprovalOutcomeState { FinalApproved, Denied, NotFinal }

public sealed record ApprovalOutcomeBindingV1
{
    public ApprovalOutcomeBindingV1(Guid tenantId,Guid processModelId,Guid versionId,string contentHash,
        Guid decisionActorId,ApprovalOutcomeState outcomeState,string fence,DateTime observedAtUtc,DateTime validUntilUtc)
    {
        TenantId=Require(tenantId,nameof(tenantId));ProcessModelId=Require(processModelId,nameof(processModelId));
        VersionId=Require(versionId,nameof(versionId));ContentHash=ValidateContentHash(contentHash);
        DecisionActorId=Require(decisionActorId,nameof(decisionActorId));
        if(!Enum.IsDefined(outcomeState))throw new ArgumentOutOfRangeException(nameof(outcomeState));
        if(string.IsNullOrEmpty(fence)||fence.Length>256||!string.Equals(fence,fence.Trim(),StringComparison.Ordinal))throw new ArgumentException("invalid_outcome_fence",nameof(fence));
        ObservedAtUtc=PublishAuthorityDecisionV1.RequireUtc(observedAtUtc,nameof(observedAtUtc));ValidUntilUtc=PublishAuthorityDecisionV1.RequireUtc(validUntilUtc,nameof(validUntilUtc));
        if(validUntilUtc<=observedAtUtc)throw new ArgumentException("invalid_outcome_interval",nameof(validUntilUtc));OutcomeState=outcomeState;Fence=fence;
    }
    public Guid TenantId{get;} public Guid ProcessModelId{get;} public Guid VersionId{get;} public string ContentHash{get;}
    public Guid DecisionActorId{get;} public ApprovalOutcomeState OutcomeState{get;} public string Fence{get;}
    public DateTime ObservedAtUtc{get;} public DateTime ValidUntilUtc{get;}
    public bool IsFreshAt(DateTime nowUtc){nowUtc=PublishAuthorityDecisionV1.RequireUtc(nowUtc,nameof(nowUtc));return nowUtc>=ObservedAtUtc&&nowUtc<ValidUntilUtc;}
    private static Guid Require(Guid value,string name)=>value==Guid.Empty?throw new ArgumentException("empty_identity",name):value;
    private static string ValidateContentHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(value.Length!=71||!value.StartsWith("sha256:",StringComparison.Ordinal)||value.AsSpan(7).ContainsAnyExcept("0123456789abcdef"))
            throw new ArgumentException("invalid_content_hash",nameof(value));
        return value;
    }
}

public sealed record ApprovalOutcomeProviderResult
{
    public ApprovalOutcomeProviderResult(ApprovalOutcomeProviderState state,ApprovalOutcomeBindingV1? binding)
    {if(!Enum.IsDefined(state))throw new ArgumentOutOfRangeException(nameof(state));if(state==ApprovalOutcomeProviderState.Available!=(binding is not null))throw new ArgumentException("outcome_binding_state_mismatch",nameof(binding));State=state;Binding=binding;}
    public ApprovalOutcomeProviderState State{get;} public ApprovalOutcomeBindingV1? Binding{get;}
    public static ApprovalOutcomeProviderResult Unavailable()=>new(ApprovalOutcomeProviderState.Unavailable,null);
}
public sealed record ApprovalOutcomeRevalidationV1(ApprovalOutcomeProviderResult Initial,ApprovalOutcomeProviderResult Revalidated);
public interface IApprovalOutcomeDecisionProvider
{
    ValueTask<ApprovalOutcomeProviderResult> ResolveAsync(ApprovalOutcomeReferenceV1 reference,PublishApprovalPolicyRequestV1 request,CancellationToken cancellationToken);
    ValueTask<ApprovalOutcomeProviderResult> RevalidateAsync(ApprovalOutcomeReferenceV1 reference,PublishApprovalPolicyRequestV1 request,string expectedFence,CancellationToken cancellationToken);
}

public sealed record PublishApprovalAuthorizationInput(
    bool Authenticated,bool TargetVisible,DateTime EvaluatedAtUtc,PublishActorProvenanceV1 Provenance,
    PublishApprovalPolicyRequestV1 InitialRequest,PublishApprovalPolicyDecisionV1 InitialDecision,
    PublishApprovalPolicyRequestV1 RevalidatedRequest,PublishApprovalPolicyDecisionV1 RevalidatedDecision,
    Fu16PublishActorProofResult ActorProof,PublishAuthorityRevalidationV1 EntitlementDecision,
    PublishAuthorityRevalidationV1 PermissionDecision,PublishAuthorityRevalidationV1 EligibilityDecision,
    ApprovalOutcomeReferenceV1? ApprovalOutcomeReference=null,ApprovalOutcomeRevalidationV1? ApprovalOutcome=null);

public sealed record PublishApprovalAuthorizationResult(int HttpStatus,string StableCode,bool ContractGatesSatisfied)
{ public bool IsExecutable=>false; }

public static class PublishApprovalAuthorizationContract
{
    public static PublishApprovalAuthorizationResult Evaluate(PublishApprovalAuthorizationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(!input.Authenticated)return Failure(401,PublishApprovalFailureCodes.AuthenticationRequired);
        if(!input.TargetVisible)return Failure(404,PublishApprovalFailureCodes.ResourceNotFound);
        foreach(var gate in new[]{input.EntitlementDecision,input.PermissionDecision})
        {var result=EvaluateAuthoritativeGate(gate,input.EvaluatedAtUtc,PublishApprovalFailureCodes.PermissionDenied);if(result is not null)return result;}
        var proofFailure=EvaluateProof(input);if(proofFailure is not null)return proofFailure;
        if(!Binds(input.Provenance,input.InitialRequest)||!Binds(input.Provenance,input.RevalidatedRequest)||input.InitialRequest!=input.RevalidatedRequest)return Failure(409,PublishApprovalFailureCodes.AuthorityStale);
        var authorityFailure=EvaluatePolicyAuthority(input.InitialDecision);if(authorityFailure is not null)return authorityFailure;
        authorityFailure=EvaluatePolicyAuthority(input.RevalidatedDecision);if(authorityFailure is not null)return authorityFailure;
        if(!input.InitialDecision.IsFreshAt(input.EvaluatedAtUtc)||!input.RevalidatedDecision.IsFreshAt(input.EvaluatedAtUtc)||input.InitialDecision.PolicyVersion!=input.RevalidatedDecision.PolicyVersion||input.InitialDecision.Requirement!=input.RevalidatedDecision.Requirement)return Failure(409,PublishApprovalFailureCodes.AuthorityStale);
        if(input.InitialRequest.PublisherActorId==input.InitialRequest.RequesterActorId||input.InitialRequest.PublisherActorId==input.InitialRequest.AuthorActorId)return Failure(403,PublishApprovalFailureCodes.SodDenied);
        var eligibility=EvaluateAuthoritativeGate(input.EligibilityDecision,input.EvaluatedAtUtc,PublishApprovalFailureCodes.SodDenied);if(eligibility is not null)return eligibility;
        if(input.InitialDecision.Requirement==PublishApprovalRequirement.Required)
        {if(input.ApprovalOutcomeReference is null||input.ApprovalOutcome is null)return Failure(400,PublishApprovalFailureCodes.InvalidRequest);var outcome=EvaluateApprovalOutcome(input);if(outcome is not null)return outcome;}
        else if(input.ApprovalOutcomeReference is not null||input.ApprovalOutcome is not null)return Failure(400,PublishApprovalFailureCodes.InvalidRequest);
        return new(503,PublishApprovalFailureCodes.RuntimeUnavailable,true);
    }

    private static PublishApprovalAuthorizationResult? EvaluateAuthoritativeGate(PublishAuthorityRevalidationV1 gate,DateTime evaluatedAtUtc,string denialCode)
    {
        ArgumentNullException.ThrowIfNull(gate);
        foreach(var decision in new[]{gate.Initial,gate.Revalidated})if(decision.State is AuthoritativeDecisionState.Unavailable or AuthoritativeDecisionState.Malformed or AuthoritativeDecisionState.Indeterminate)return Failure(503,PublishApprovalFailureCodes.AuthorityUnavailable);
        if(!gate.Initial.IsFreshAt(evaluatedAtUtc)||!gate.Revalidated.IsFreshAt(evaluatedAtUtc)||gate.Initial.Version!=gate.Revalidated.Version||!string.Equals(gate.Initial.Fence,gate.Revalidated.Fence,StringComparison.Ordinal)||gate.Initial.State!=gate.Revalidated.State)return Failure(409,PublishApprovalFailureCodes.AuthorityStale);
        return gate.Initial.State==AuthoritativeDecisionState.Denied?Failure(403,denialCode):null;
    }
    private static PublishApprovalAuthorizationResult? EvaluateProof(PublishApprovalAuthorizationInput input)=>input.ActorProof.State switch
    {Fu16PublishActorProofState.Available when input.ActorProof.Fresh&&input.ActorProof.RequestBound&&input.ActorProof.EffectiveActorId==input.InitialRequest.PublisherActorId=>null,Fu16PublishActorProofState.Denied=>Failure(403,PublishApprovalFailureCodes.PermissionDenied),Fu16PublishActorProofState.Unavailable=>Failure(503,PublishApprovalFailureCodes.Fu16OnboardingUnavailable),_=>Failure(503,PublishApprovalFailureCodes.AuthorityUnavailable)};
    private static PublishApprovalAuthorizationResult? EvaluatePolicyAuthority(PublishApprovalPolicyDecisionV1 decision)=>decision.AuthorityState==PublishApprovalAuthorityState.Available?null:Failure(503,PublishApprovalFailureCodes.AuthorityUnavailable);
    private static PublishApprovalAuthorizationResult? EvaluateApprovalOutcome(PublishApprovalAuthorizationInput input)
    {
        var pair=input.ApprovalOutcome!;
        foreach(var result in new[]{pair.Initial,pair.Revalidated})switch(result.State){case ApprovalOutcomeProviderState.NotFound:return Failure(404,PublishApprovalFailureCodes.ResourceNotFound);case ApprovalOutcomeProviderState.Unavailable:case ApprovalOutcomeProviderState.Malformed:case ApprovalOutcomeProviderState.Indeterminate:return Failure(503,PublishApprovalFailureCodes.AuthorityUnavailable);}
        var initial=pair.Initial.Binding!;var revalidated=pair.Revalidated.Binding!;
        if(!Binds(input.InitialRequest,initial)||!Binds(input.RevalidatedRequest,revalidated)||!initial.IsFreshAt(input.EvaluatedAtUtc)||!revalidated.IsFreshAt(input.EvaluatedAtUtc)||!string.Equals(initial.Fence,revalidated.Fence,StringComparison.Ordinal)||initial.OutcomeState!=revalidated.OutcomeState||initial.DecisionActorId!=revalidated.DecisionActorId)return Failure(409,PublishApprovalFailureCodes.AuthorityStale);
        if(initial.OutcomeState is ApprovalOutcomeState.Denied or ApprovalOutcomeState.NotFinal)return Failure(403,PublishApprovalFailureCodes.PermissionDenied);
        return initial.DecisionActorId==input.InitialRequest.PublisherActorId?Failure(403,PublishApprovalFailureCodes.SodDenied):null;
    }
    private static bool Binds(PublishActorProvenanceV1 provenance,PublishApprovalPolicyRequestV1 request)=>provenance.TenantId==request.TenantId&&provenance.ProcessModelId==request.ModelId&&provenance.ProcessModelVersionId==request.VersionId&&string.Equals(provenance.ContentHash,request.ContentHash,StringComparison.Ordinal)&&provenance.ModelAuthorActorId==request.AuthorActorId&&provenance.PublishRequesterActorId==request.RequesterActorId;
    private static bool Binds(PublishApprovalPolicyRequestV1 request,ApprovalOutcomeBindingV1 binding)=>request.TenantId==binding.TenantId&&request.ModelId==binding.ProcessModelId&&request.VersionId==binding.VersionId&&string.Equals(request.ContentHash,binding.ContentHash,StringComparison.Ordinal);
    private static PublishApprovalAuthorizationResult Failure(int status,string code)=>new(status,code,false);
}
