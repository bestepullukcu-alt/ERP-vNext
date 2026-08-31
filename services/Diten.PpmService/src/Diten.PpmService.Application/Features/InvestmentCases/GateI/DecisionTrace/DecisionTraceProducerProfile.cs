using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public static class DecisionTraceProducerProfile
{
    public const string Operation = "decision-registry.decision-references.validate.v1";
    public const string Permission = "management-governance.decision-references.validate";
    public const string Audience = "diten-management-governance-service";
    public const string ClientId = "diten.management-governance";
    public const string Issuer = "diten-auth-service";
    public const string OwnerModule = "MOD-0007";
    public const string ProtocolScope = "diten.s2s.delegated.invoke";
    public const string TokenFamily = "DelegatedActorProofV1";
}
