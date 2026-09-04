using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public sealed record S2SFundingScenarioContextV1(
    S2SAuthenticationState AuthenticationState,
    S2SAuthorizationState EntitlementState,
    S2SAuthorizationState ExplicitGrantState,
    S2SFreshnessState FreshnessState,
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
    S2SVersionFenceV1 InitialFence,
    S2SVersionFenceV1 RevalidatedFence,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ValidUntilUtc,
    DateTimeOffset RevalidatedAtUtc);
