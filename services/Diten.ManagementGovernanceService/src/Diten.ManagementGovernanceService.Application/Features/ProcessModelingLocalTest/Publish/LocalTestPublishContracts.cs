using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Publish;

public sealed record LocalTestPublishTargetSnapshot(
    Guid TenantId,
    Guid ProcessModelId,
    Guid ProcessModelVersionId,
    string ContentHash,
    Guid ModelAuthorActorId,
    int Version,
    string LifecycleState);

public sealed record LocalTestPublishAuthorizationEvidence(
    PublishApprovalAuthorizationInput Input,
    LocalTestExplicitGrantEvidence ExplicitGrant,
    string CompositeFence,
    string BoundRequestHash)
{
    public LocalTestPublishAuthorizationEvidence Validate()
    {
        ArgumentNullException.ThrowIfNull(Input);
        ArgumentNullException.ThrowIfNull(ExplicitGrant);
        if(string.IsNullOrEmpty(CompositeFence)||CompositeFence.Length>512||
           !string.Equals(CompositeFence,CompositeFence.Trim(),StringComparison.Ordinal))
            throw new ArgumentException("process_model_publish_composite_fence_invalid",nameof(CompositeFence));
        if(BoundRequestHash.Length!=64||BoundRequestHash.AsSpan().ContainsAnyExcept("0123456789abcdef"))
            throw new ArgumentException("process_model_publish_request_hash_invalid",nameof(BoundRequestHash));
        return this;
    }
}

public sealed record LocalTestExplicitGrantEvidence(
    Guid TenantId,
    string ModuleCode,
    string Permission,
    Guid EffectiveActorId,
    Guid ServicePrincipalId,
    string RequestHash,
    PublishAuthorityRevalidationV1 Decision)
{
    public const string ExpectedModuleCode = "MOD-0355";
}

public interface ILocalTestPublishTargetReader
{
    Task<LocalTestPublishTargetSnapshot?> FindAsync(Guid tenantId,Guid processModelVersionId,CancellationToken cancellationToken);
}

public interface ILocalTestPublishAuthorizationEvidenceProvider
{
    Task<LocalTestPublishAuthorizationEvidence> ResolveAsync(
        LocalTestPublishTargetSnapshot target,
        LocalTestCommandContext requestContext,
        CancellationToken cancellationToken);

    Task<LocalTestPublishAuthorizationEvidence> RevalidateAsync(
        LocalTestPublishTargetSnapshot target,
        LocalTestCommandContext requestContext,
        string expectedCompositeFence,
        CancellationToken cancellationToken);
}

public sealed class UnavailableLocalTestPublishAuthorizationEvidenceProvider : ILocalTestPublishAuthorizationEvidenceProvider
{
    public Task<LocalTestPublishAuthorizationEvidence> ResolveAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,CancellationToken cancellationToken)
        =>Unavailable(cancellationToken);

    public Task<LocalTestPublishAuthorizationEvidence> RevalidateAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,string expectedCompositeFence,CancellationToken cancellationToken)
        =>Unavailable(cancellationToken);

    private static Task<LocalTestPublishAuthorizationEvidence> Unavailable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new LocalTestPublishAuthorizationException(503,PublishApprovalFailureCodes.RuntimeUnavailable);
    }
}

public sealed class LocalTestPublishAuthorizationException(int statusCode,string stableCode):Exception(stableCode)
{
    public int StatusCode { get; }=statusCode;
    public string StableCode { get; }=stableCode;
}

public sealed record LocalTestPublishGateResult(int HttpStatus,string StableCode,bool IsExecutable);

public static class LocalTestNotRequiredPublishGate
{
    public const string Authorized = "process_model_publish_local_test_authorized";

    public static LocalTestPublishGateResult Evaluate(LocalTestPublishAuthorizationEvidence evidence,DateTime evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        evaluatedAtUtc=PublishAuthorityDecisionV1.RequireUtc(evaluatedAtUtc,nameof(evaluatedAtUtc));
        var input=evidence.Input with{EvaluatedAtUtc=evaluatedAtUtc};
        var explicitGrant=EvaluateExplicitGrant(evidence.ExplicitGrant.Decision,evaluatedAtUtc);
        if(explicitGrant is not null)return explicitGrant;
        if(input.InitialDecision.Requirement==PublishApprovalRequirement.Required||
           input.RevalidatedDecision.Requirement==PublishApprovalRequirement.Required)
            return new(503,PublishApprovalFailureCodes.RuntimeUnavailable,false);
        var result=PublishApprovalAuthorizationContract.Evaluate(input);
        return result.HttpStatus==503&&result.ContractGatesSatisfied&&
               string.Equals(result.StableCode,PublishApprovalFailureCodes.RuntimeUnavailable,StringComparison.Ordinal)
            ? new(204,Authorized,true)
            : new(result.HttpStatus,result.StableCode,false);
    }

    public static LocalTestPublishGateResult Revalidate(
        LocalTestPublishAuthorizationEvidence initial,
        LocalTestPublishAuthorizationEvidence revalidated,
        DateTime evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(initial);ArgumentNullException.ThrowIfNull(revalidated);
        if(!string.Equals(initial.CompositeFence,revalidated.CompositeFence,StringComparison.Ordinal)||
           !string.Equals(initial.BoundRequestHash,revalidated.BoundRequestHash,StringComparison.Ordinal)||
           initial.Input.Provenance!=revalidated.Input.Provenance||
           initial.Input.InitialRequest!=revalidated.Input.InitialRequest||
           initial.Input.RevalidatedRequest!=revalidated.Input.RevalidatedRequest||
           !Stable(initial.Input.InitialDecision,revalidated.Input.InitialDecision)||
           !Stable(initial.Input.RevalidatedDecision,revalidated.Input.RevalidatedDecision)||
           initial.Input.ActorProof!=revalidated.Input.ActorProof||
           !Stable(initial.Input.EntitlementDecision,revalidated.Input.EntitlementDecision)||
           !Stable(initial.Input.PermissionDecision,revalidated.Input.PermissionDecision)||
           !Stable(initial.Input.EligibilityDecision,revalidated.Input.EligibilityDecision)||
           !Stable(initial.ExplicitGrant,revalidated.ExplicitGrant))
            return new(409,PublishApprovalFailureCodes.AuthorityStale,false);
        return Evaluate(revalidated,evaluatedAtUtc);
    }

    public static LocalTestPublishGateResult ValidateBindings(
        LocalTestPublishAuthorizationEvidence evidence,
        LocalTestPublishTargetSnapshot target,
        LocalTestCommandContext context)
    {
        var input=evidence.Input;var provenance=input.Provenance;var grant=evidence.ExplicitGrant;
        if(!string.Equals(evidence.BoundRequestHash,context.RequestHash,StringComparison.Ordinal)||
           provenance.TenantId!=target.TenantId||provenance.ProcessModelId!=target.ProcessModelId||
           provenance.ProcessModelVersionId!=target.ProcessModelVersionId||
           !string.Equals(provenance.ContentHash,target.ContentHash,StringComparison.Ordinal)||
           provenance.ModelAuthorActorId!=target.ModelAuthorActorId||
           provenance.PublishRequesterActorId!=context.ActorId||
           grant.TenantId!=target.TenantId||
           !string.Equals(grant.ModuleCode,LocalTestExplicitGrantEvidence.ExpectedModuleCode,StringComparison.Ordinal)||
           !string.Equals(grant.Permission,context.Permission,StringComparison.Ordinal)||
           grant.EffectiveActorId!=input.InitialRequest.PublisherActorId||
           context.ServicePrincipalId==Guid.Empty||grant.ServicePrincipalId!=context.ServicePrincipalId||
           !string.Equals(grant.RequestHash,context.RequestHash,StringComparison.Ordinal))
            return new(409,PublishApprovalFailureCodes.AuthorityStale,false);
        return new(204,Authorized,true);
    }

    private static LocalTestPublishGateResult? EvaluateExplicitGrant(PublishAuthorityRevalidationV1 gate,DateTime evaluatedAtUtc)
    {
        foreach(var decision in new[]{gate.Initial,gate.Revalidated})
            if(decision.State is AuthoritativeDecisionState.Unavailable or AuthoritativeDecisionState.Malformed or AuthoritativeDecisionState.Indeterminate)
                return new(503,PublishApprovalFailureCodes.AuthorityUnavailable,false);
        if(!gate.Initial.IsFreshAt(evaluatedAtUtc)||!gate.Revalidated.IsFreshAt(evaluatedAtUtc)||
           gate.Initial.Version!=gate.Revalidated.Version||
           !string.Equals(gate.Initial.Fence,gate.Revalidated.Fence,StringComparison.Ordinal)||
           gate.Initial.State!=gate.Revalidated.State)
            return new(409,PublishApprovalFailureCodes.AuthorityStale,false);
        return gate.Initial.State==AuthoritativeDecisionState.Denied
            ? new(403,PublishApprovalFailureCodes.PermissionDenied,false)
            : null;
    }

    private static bool Stable(PublishApprovalPolicyDecisionV1 left,PublishApprovalPolicyDecisionV1 right)=>
        left.AuthorityState==right.AuthorityState&&left.Requirement==right.Requirement&&left.PolicyVersion==right.PolicyVersion&&
        left.ObservedAtUtc==right.ObservedAtUtc&&left.ValidUntilUtc==right.ValidUntilUtc;
    private static bool Stable(PublishAuthorityRevalidationV1 left,PublishAuthorityRevalidationV1 right)=>
        Stable(left.Initial,right.Initial)&&Stable(left.Revalidated,right.Revalidated);
    private static bool Stable(PublishAuthorityDecisionV1 left,PublishAuthorityDecisionV1 right)=>
        left.State==right.State&&left.Version==right.Version&&string.Equals(left.Fence,right.Fence,StringComparison.Ordinal)&&
        left.ObservedAtUtc==right.ObservedAtUtc&&left.ValidUntilUtc==right.ValidUntilUtc;
    private static bool Stable(LocalTestExplicitGrantEvidence left,LocalTestExplicitGrantEvidence right)=>
        left.TenantId==right.TenantId&&string.Equals(left.ModuleCode,right.ModuleCode,StringComparison.Ordinal)&&
        string.Equals(left.Permission,right.Permission,StringComparison.Ordinal)&&left.EffectiveActorId==right.EffectiveActorId&&
        left.ServicePrincipalId==right.ServicePrincipalId&&string.Equals(left.RequestHash,right.RequestHash,StringComparison.Ordinal)&&
        Stable(left.Decision,right.Decision);
}
