using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.BenefitRealization;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.PpmService.Domain.GateI.FundingScenario;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public sealed record GateITrustedMutationContext(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid DelegatedActorId,
    Guid DelegationId,
    Guid ServicePrincipalId,
    Guid CredentialId,
    string ClientId,
    string Issuer,
    string Audience,
    string TokenType,
    string ProtocolScope,
    string OperationId,
    IReadOnlyList<string> Permissions,
    string RequestHash,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration);
