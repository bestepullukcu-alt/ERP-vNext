using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public static class FundingScenarioContractEvaluator
{
    public static FundingScenarioContractResult EvaluateBudgetBytes(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioValidationMode mode,S2SFundingScenarioContextV1 context,ProducerReferenceValidationResult producer)
    {
        var security=EvaluateSecurity(FundingScenarioAtomicLane.Budgeting,context,wrapperUtf8.Span);if(security is not null)return security;
        if(!Enum.IsDefined(mode))return Fail(400,FundingScenarioFailureCodes.InvalidRequest);
        try{_ = SelectedBudgetVersionReferenceV1.ParseExact(Encoding.UTF8.GetString(wrapperUtf8.Span));}
        catch(Exception exception) when(exception is ArgumentException or FormatException or System.Text.Json.JsonException){return Fail(400,FundingScenarioFailureCodes.InvalidRequest);}
        return EvaluateProducer(producer);
    }

    public static FundingScenarioContractResult EvaluateScenarioBytes(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioReferenceKind kind,FundingScenarioValidationMode mode,S2SFundingScenarioContextV1 context,ProducerReferenceValidationResult producer)
    {
        var security=EvaluateSecurity(FundingScenarioAtomicLane.ScenarioPlanning,context,wrapperUtf8.Span);if(security is not null)return security;
        if(!Enum.IsDefined(kind)||!Enum.IsDefined(mode)||kind==FundingScenarioReferenceKind.SelectedBudget)return Fail(400,FundingScenarioFailureCodes.InvalidRequest);
        if(mode==FundingScenarioValidationMode.CurrentSelectionEligibility&&kind!=FundingScenarioReferenceKind.SelectedScenario)return Fail(400,FundingScenarioFailureCodes.InvalidRequest);
        try
        {
            var json=Encoding.UTF8.GetString(wrapperUtf8.Span);
            _ = kind switch{FundingScenarioReferenceKind.ScenarioVersion=>(object)InvestmentCaseScenarioVersionReferenceV1.ParseExact(json),FundingScenarioReferenceKind.ComparatorOutput=>InvestmentCaseComparatorOutputReferenceV1.ParseExact(json),FundingScenarioReferenceKind.SelectedScenario=>SelectedScenarioReferenceV1.ParseExact(json),_=>throw new FormatException("unsupported_kind")};
        }
        catch(Exception exception) when(exception is ArgumentException or FormatException or System.Text.Json.JsonException){return Fail(400,FundingScenarioFailureCodes.InvalidRequest);}
        return EvaluateProducer(producer);
    }

    private static FundingScenarioContractResult? EvaluateSecurity(FundingScenarioProducerProfile profile,S2SFundingScenarioContextV1 context,ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(context);
        if(!Enum.IsDefined(context.AuthenticationState)||!Enum.IsDefined(context.EntitlementState)||!Enum.IsDefined(context.ExplicitGrantState)||!Enum.IsDefined(context.FreshnessState))return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        if(context.AuthenticationState==S2SAuthenticationState.Invalid)return Fail(401,FundingScenarioFailureCodes.AuthenticationRequired);
        if(context.AuthenticationState is S2SAuthenticationState.Unavailable or S2SAuthenticationState.Malformed or S2SAuthenticationState.Indeterminate)return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        var receiver=S2SOutboundReceiverProfiles.ForOwner(profile.OwnerModule);
        if(context.TenantId==Guid.Empty||context.EffectiveActorId==Guid.Empty||context.DelegatedActorId is null||context.DelegatedActorId==Guid.Empty||context.DelegatedActorId!=context.EffectiveActorId||!context.DelegatedProofValidated||!string.Equals(context.Audience,profile.Audience,StringComparison.Ordinal)||!string.Equals(context.ClientId,profile.ClientId,StringComparison.Ordinal)||!string.Equals(context.ProtocolScope,profile.ProtocolScope,StringComparison.Ordinal)||!string.Equals(context.Method,receiver.Method,StringComparison.Ordinal)||!string.Equals(context.Path,receiver.Path,StringComparison.Ordinal)||!FundingScenarioRequestBinding.IsWellFormed(context.Method,context.Path,context.RequestHash))return Fail(401,FundingScenarioFailureCodes.AuthenticationRequired);
        var actual=FundingScenarioRequestBinding.Compute(context.Method,context.Path,context.TenantId,context.OperationId,body);
        if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual),Encoding.ASCII.GetBytes(context.RequestHash)))return Fail(401,FundingScenarioFailureCodes.AuthenticationRequired);
        if(context.EntitlementState==S2SAuthorizationState.Denied||context.ExplicitGrantState==S2SAuthorizationState.Denied)return Fail(403,FundingScenarioFailureCodes.Forbidden);
        if(context.EntitlementState is S2SAuthorizationState.Unavailable or S2SAuthorizationState.Malformed or S2SAuthorizationState.Indeterminate||context.ExplicitGrantState is S2SAuthorizationState.Unavailable or S2SAuthorizationState.Malformed or S2SAuthorizationState.Indeterminate)return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        if(!string.Equals(context.OwnerModule,profile.OwnerModule,StringComparison.Ordinal)||!string.Equals(context.OperationId,profile.OperationId,StringComparison.Ordinal)||!string.Equals(context.Permission,profile.Permission,StringComparison.Ordinal))return Fail(403,FundingScenarioFailureCodes.Forbidden);
        if(context.FreshnessState==S2SFreshnessState.Stale||context.InitialFence!=context.RevalidatedFence||context.ObservedAtUtc>context.ValidUntilUtc||context.RevalidatedAtUtc>context.ValidUntilUtc)return Fail(409,FundingScenarioFailureCodes.Conflict);
        if(context.FreshnessState is S2SFreshnessState.Unavailable or S2SFreshnessState.Malformed or S2SFreshnessState.Indeterminate||!ValidFence(context.InitialFence))return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        return null;
    }

    private static FundingScenarioContractResult EvaluateProducer(ProducerReferenceValidationResult producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if(!string.Equals(producer.ResponseVersion,"1.0",StringComparison.Ordinal))return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        return producer.State switch{ProducerReferenceState.Allowed=>new(503,FundingScenarioFailureCodes.RuntimeUnavailable,true),ProducerReferenceState.MissingOrInvisible=>Fail(404,FundingScenarioFailureCodes.NotFound),ProducerReferenceState.IneligibleOrStale=>Fail(409,FundingScenarioFailureCodes.Conflict),ProducerReferenceState.Unavailable or ProducerReferenceState.Malformed or ProducerReferenceState.Indeterminate or ProducerReferenceState.UnsupportedVersion=>Fail(503,FundingScenarioFailureCodes.ProviderUnavailable),_=>Fail(503,FundingScenarioFailureCodes.ProviderUnavailable)};
    }
    private static bool ValidFence(S2SVersionFenceV1 fence)=>fence is not null&&fence.PrincipalVersion>0&&fence.CredentialGeneration>0&&fence.AuthorizationVersion>0&&!string.IsNullOrWhiteSpace(fence.EntitlementVersion);
    private static FundingScenarioContractResult Fail(int status,string code)=>new(status,code,false);
}
