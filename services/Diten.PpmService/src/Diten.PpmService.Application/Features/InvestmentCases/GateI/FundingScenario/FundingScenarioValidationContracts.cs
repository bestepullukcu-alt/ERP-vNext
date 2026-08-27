using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;

public enum FundingScenarioValidationMode { HistoricalResolve, NewReferenceEligibility, CurrentSelectionEligibility }
public enum Fu16AuthenticationState { AuthenticatedS2SFamilyValidated, Invalid, Unavailable, Malformed, Indeterminate }
public enum Fu16AuthorizationState { Allowed, Denied, Unavailable, Malformed, Indeterminate }
public enum Fu16FreshnessState { Current, Stale, Unavailable, Malformed, Indeterminate }
public enum ProducerReferenceState { Allowed, MissingOrInvisible, IneligibleOrStale, Unavailable, Malformed, Indeterminate, UnsupportedVersion }
public enum FundingScenarioReferenceKind { SelectedBudget, ScenarioVersion, ComparatorOutput, SelectedScenario }

public sealed record FundingScenarioProducerProfile(
    string OwnerModule,string OperationId,string Permission,string Audience,string ClientId,string ProtocolScope,
    string FixtureClosure,string CoreCheckpoint,string SigningIdentity,string FixtureKeyId,string SigningVectorPath);

public static class FundingScenarioAtomicLane
{
    public static readonly FundingScenarioProducerProfile Budgeting=new("MOD-0136","budgeting.budget-version-references.validate","budgeting.budget-version-references.validate","diten-fpa-service","diten.fpa","diten.s2s.delegated.invoke","711962a3fdc1226d947672dc9b48d29296c960a0","1949b93ead3dc1ac3234673bbe00ed67e3615743","diten.fpa.budgeting.audit","budgeting-mod-0136-fixture-current.test-only","execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0136/audit/budgeting-audit-intent-submitted-v1.signing-vector.json");
    public static readonly FundingScenarioProducerProfile ScenarioPlanning=new("MOD-0138","fpa.scenario-planning.references.validate","fpa.scenario-planning.references.validate","diten-fpa-service","diten.fpa","diten.s2s.delegated.invoke","3df680d6e006bfce19e382253ddd1f2f873c2295","acae87090f35e5e0a7f37ad66dd8e98fc69c07bb","diten.fpa.mod-0138.scenario-planning","mod-0138-scenario-planning-fixture-current","execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0138/audit/scenario-planning-audit-intent-submitted-v1.signing-vector.json");
    public static IReadOnlyList<FundingScenarioProducerProfile> RequiredProfiles=>[Budgeting,ScenarioPlanning];
}

public sealed record Fu16VersionFenceV1(long PrincipalVersion,long CredentialGeneration,long AuthorizationVersion,string EntitlementVersion);

public sealed record Fu16FundingScenarioContextV1(
    Fu16AuthenticationState AuthenticationState,
    Fu16AuthorizationState EntitlementState,
    Fu16AuthorizationState ExplicitGrantState,
    Fu16FreshnessState FreshnessState,
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    bool DelegatedProofValidated,
    string Audience,
    string ClientId,
    string OwnerModule,
    string OperationId,
    string Permission,
    string ProtocolScope,
    string Method,
    string Path,
    string RequestHash,
    Fu16VersionFenceV1 InitialFence,
    Fu16VersionFenceV1 RevalidatedFence,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ValidUntilUtc,
    DateTimeOffset RevalidatedAtUtc);

public sealed record BudgetReferenceValidationRequest(SelectedBudgetVersionReferenceV1 Wrapper,FundingScenarioValidationMode Mode);
public sealed record ScenarioReferenceValidationRequest(object Wrapper,FundingScenarioReferenceKind Kind,FundingScenarioValidationMode Mode);
public sealed record ProducerReferenceValidationResult(ProducerReferenceState State,string ResponseVersion="1.0");

public interface IBudgetVersionReferenceValidationPort
{ ValueTask<ProducerReferenceValidationResult> ValidateAsync(BudgetReferenceValidationRequest request,Fu16FundingScenarioContextV1 context,CancellationToken cancellationToken); }
public interface IScenarioPlanningReferenceValidationPort
{ ValueTask<ProducerReferenceValidationResult> ValidateAsync(ScenarioReferenceValidationRequest request,Fu16FundingScenarioContextV1 context,CancellationToken cancellationToken); }

public sealed record FundingScenarioContractResult(int HttpStatus,string StableCode,bool ContractSatisfied)
{ public bool IsExecutable=>false; }

public static class FundingScenarioFailureCodes
{
    public const string InvalidRequest="ppm_gate_ib_invalid_request",AuthenticationRequired="ppm_gate_ib_authentication_required",Forbidden="ppm_gate_ib_forbidden",NotFound="ppm_gate_ib_not_found",Conflict="ppm_gate_ib_conflict",ProviderUnavailable="ppm_gate_ib_provider_unavailable",RuntimeUnavailable="ppm_gate_ib_runtime_unavailable";
}

public static class FundingScenarioContractEvaluator
{
    public static FundingScenarioContractResult EvaluateBudgetBytes(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioValidationMode mode,Fu16FundingScenarioContextV1 context,ProducerReferenceValidationResult producer)
    {
        var security=EvaluateSecurity(FundingScenarioAtomicLane.Budgeting,context,wrapperUtf8.Span);if(security is not null)return security;
        if(!Enum.IsDefined(mode))return Fail(400,FundingScenarioFailureCodes.InvalidRequest);
        try{_ = SelectedBudgetVersionReferenceV1.ParseExact(Encoding.UTF8.GetString(wrapperUtf8.Span));}
        catch(Exception exception) when(exception is ArgumentException or FormatException or System.Text.Json.JsonException){return Fail(400,FundingScenarioFailureCodes.InvalidRequest);}
        return EvaluateProducer(producer);
    }

    public static FundingScenarioContractResult EvaluateScenarioBytes(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioReferenceKind kind,FundingScenarioValidationMode mode,Fu16FundingScenarioContextV1 context,ProducerReferenceValidationResult producer)
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

    private static FundingScenarioContractResult? EvaluateSecurity(FundingScenarioProducerProfile profile,Fu16FundingScenarioContextV1 context,ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(context);
        if(!Enum.IsDefined(context.AuthenticationState)||!Enum.IsDefined(context.EntitlementState)||!Enum.IsDefined(context.ExplicitGrantState)||!Enum.IsDefined(context.FreshnessState))return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        if(context.AuthenticationState==Fu16AuthenticationState.Invalid)return Fail(401,FundingScenarioFailureCodes.AuthenticationRequired);
        if(context.AuthenticationState is Fu16AuthenticationState.Unavailable or Fu16AuthenticationState.Malformed or Fu16AuthenticationState.Indeterminate)return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        var receiver=Fu16OutboundReceiverProfiles.ForOwner(profile.OwnerModule);
        if(context.TenantId==Guid.Empty||context.EffectiveActorId==Guid.Empty||context.DelegatedActorId is null||context.DelegatedActorId==Guid.Empty||context.DelegatedActorId!=context.EffectiveActorId||!context.DelegatedProofValidated||!string.Equals(context.Audience,profile.Audience,StringComparison.Ordinal)||!string.Equals(context.ClientId,profile.ClientId,StringComparison.Ordinal)||!string.Equals(context.ProtocolScope,profile.ProtocolScope,StringComparison.Ordinal)||!string.Equals(context.Method,receiver.Method,StringComparison.Ordinal)||!string.Equals(context.Path,receiver.Path,StringComparison.Ordinal)||!FundingScenarioRequestBinding.IsWellFormed(context.Method,context.Path,context.RequestHash))return Fail(401,FundingScenarioFailureCodes.AuthenticationRequired);
        var actual=FundingScenarioRequestBinding.Compute(context.Method,context.Path,context.TenantId,context.OperationId,body);
        if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual),Encoding.ASCII.GetBytes(context.RequestHash)))return Fail(401,FundingScenarioFailureCodes.AuthenticationRequired);
        if(context.EntitlementState==Fu16AuthorizationState.Denied||context.ExplicitGrantState==Fu16AuthorizationState.Denied)return Fail(403,FundingScenarioFailureCodes.Forbidden);
        if(context.EntitlementState is Fu16AuthorizationState.Unavailable or Fu16AuthorizationState.Malformed or Fu16AuthorizationState.Indeterminate||context.ExplicitGrantState is Fu16AuthorizationState.Unavailable or Fu16AuthorizationState.Malformed or Fu16AuthorizationState.Indeterminate)return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        if(!string.Equals(context.OwnerModule,profile.OwnerModule,StringComparison.Ordinal)||!string.Equals(context.OperationId,profile.OperationId,StringComparison.Ordinal)||!string.Equals(context.Permission,profile.Permission,StringComparison.Ordinal))return Fail(403,FundingScenarioFailureCodes.Forbidden);
        if(context.FreshnessState==Fu16FreshnessState.Stale||context.InitialFence!=context.RevalidatedFence||context.ObservedAtUtc>context.ValidUntilUtc||context.RevalidatedAtUtc>context.ValidUntilUtc)return Fail(409,FundingScenarioFailureCodes.Conflict);
        if(context.FreshnessState is Fu16FreshnessState.Unavailable or Fu16FreshnessState.Malformed or Fu16FreshnessState.Indeterminate||!ValidFence(context.InitialFence))return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        return null;
    }

    private static FundingScenarioContractResult EvaluateProducer(ProducerReferenceValidationResult producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if(!string.Equals(producer.ResponseVersion,"1.0",StringComparison.Ordinal))return Fail(503,FundingScenarioFailureCodes.ProviderUnavailable);
        return producer.State switch{ProducerReferenceState.Allowed=>new(503,FundingScenarioFailureCodes.RuntimeUnavailable,true),ProducerReferenceState.MissingOrInvisible=>Fail(404,FundingScenarioFailureCodes.NotFound),ProducerReferenceState.IneligibleOrStale=>Fail(409,FundingScenarioFailureCodes.Conflict),ProducerReferenceState.Unavailable or ProducerReferenceState.Malformed or ProducerReferenceState.Indeterminate or ProducerReferenceState.UnsupportedVersion=>Fail(503,FundingScenarioFailureCodes.ProviderUnavailable),_=>Fail(503,FundingScenarioFailureCodes.ProviderUnavailable)};
    }
    private static bool ValidFence(Fu16VersionFenceV1 fence)=>fence is not null&&fence.PrincipalVersion>0&&fence.CredentialGeneration>0&&fence.AuthorizationVersion>0&&!string.IsNullOrWhiteSpace(fence.EntitlementVersion);
    private static FundingScenarioContractResult Fail(int status,string code)=>new(status,code,false);
}

public sealed class FundingScenarioContractValidator(IBudgetVersionReferenceValidationPort budgetPort,IScenarioPlanningReferenceValidationPort scenarioPort)
{
    public async ValueTask<FundingScenarioContractResult> ValidateBudgetAsync(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioValidationMode mode,Fu16FundingScenarioContextV1 context,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();var preflight=FundingScenarioContractEvaluator.EvaluateBudgetBytes(wrapperUtf8,mode,context,new(ProducerReferenceState.Allowed));if(!preflight.ContractSatisfied)return preflight;
        var wrapper=SelectedBudgetVersionReferenceV1.ParseExact(Encoding.UTF8.GetString(wrapperUtf8.Span));var producer=await budgetPort.ValidateAsync(new(wrapper,mode),context,cancellationToken).ConfigureAwait(false);return FundingScenarioContractEvaluator.EvaluateBudgetBytes(wrapperUtf8,mode,context,producer);
    }
    public async ValueTask<FundingScenarioContractResult> ValidateScenarioAsync(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioReferenceKind kind,FundingScenarioValidationMode mode,Fu16FundingScenarioContextV1 context,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();var preflight=FundingScenarioContractEvaluator.EvaluateScenarioBytes(wrapperUtf8,kind,mode,context,new(ProducerReferenceState.Allowed));if(!preflight.ContractSatisfied)return preflight;var json=Encoding.UTF8.GetString(wrapperUtf8.Span);object wrapper=kind switch{FundingScenarioReferenceKind.ScenarioVersion=>InvestmentCaseScenarioVersionReferenceV1.ParseExact(json),FundingScenarioReferenceKind.ComparatorOutput=>InvestmentCaseComparatorOutputReferenceV1.ParseExact(json),FundingScenarioReferenceKind.SelectedScenario=>SelectedScenarioReferenceV1.ParseExact(json),_=>throw new FormatException("unsupported_kind")};var producer=await scenarioPort.ValidateAsync(new(wrapper,kind,mode),context,cancellationToken).ConfigureAwait(false);return FundingScenarioContractEvaluator.EvaluateScenarioBytes(wrapperUtf8,kind,mode,context,producer);
    }
}

public static class FundingScenarioRequestBinding
{
    public static bool IsWellFormed(string method,string path,string hash)=>!string.IsNullOrEmpty(method)&&string.Equals(method,method.ToUpperInvariant(),StringComparison.Ordinal)&&!string.IsNullOrEmpty(path)&&path.StartsWith("/",StringComparison.Ordinal)&&!path.Contains('?',StringComparison.Ordinal)&&Fu16OutboundCanonicalRequestBinding.IsLowerHex64(hash);
    public static string Compute(string method,string path,Guid tenantId,string operation,ReadOnlySpan<byte> body)
    {
        var profile=FundingScenarioAtomicLane.RequiredProfiles.SingleOrDefault(candidate=>string.Equals(candidate.OperationId,operation,StringComparison.Ordinal))??throw new ArgumentException("invalid_request_binding_input");
        return Fu16OutboundCanonicalRequestBinding.Compute(method,path,body,tenantId,operation,[profile.Permission]);
    }
}
