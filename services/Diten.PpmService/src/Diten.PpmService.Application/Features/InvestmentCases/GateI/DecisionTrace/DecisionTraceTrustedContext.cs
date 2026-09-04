using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public sealed record DecisionTraceTrustedContext(
    Guid TenantId, Guid EffectiveActorId, Guid CorrelationId,
    string Issuer, string Audience, string ClientId, string TokenFamily, string ProtocolScope,
    string OwnerModule, string Operation, string Permission, string RequestHash,
    bool AuthenticatedServiceFamily, bool TokenFamilyValidated, bool DelegatedActorProofValidated,
    Guid DelegatedActorId, TrustedAuthorityState EntitlementState, TrustedAuthorityState ExplicitTenantGrantState,
    TrustedAuthorityState PrincipalFreshness, TrustedAuthorityState CredentialFreshness, TrustedAuthorityState AuthorizationFreshness)
{
    public bool HasRequiredIdentifiers => TenantId != Guid.Empty && EffectiveActorId != Guid.Empty && CorrelationId != Guid.Empty;
}
