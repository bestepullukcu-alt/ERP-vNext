using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;


public sealed record GateIS2SServerContext(
    bool IsAuthenticated,
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    bool DelegationVerified,
    string Audience,
    string ClientId,
    string OwnerModule,
    string Scope,
    string Method,
    string Path,
    string Operation,
    string Permission,
    string RequestHash,
    bool EntitlementGranted);
