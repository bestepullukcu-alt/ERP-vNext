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


public sealed record GateIAuthorityValidationRequest(
    GateIRelationshipKind Kind,
    GateIRelationshipAction Action,
    GateITrustedMutationContext TrustedContext,
    string OperationId,
    ReadOnlyMemory<byte> CanonicalWrapperUtf8)
{
    public Guid TenantId => TrustedContext.TenantId;
    public Guid ActorId => TrustedContext.EffectiveActorId;
    public S2SOutboundReceiverProfile Receiver => Kind switch
    {
        GateIRelationshipKind.GoverningDecision or GateIRelationshipKind.SupportingDecision =>
            S2SOutboundReceiverProfiles.DecisionRegistry,
        GateIRelationshipKind.SelectedBudgetVersion => S2SOutboundReceiverProfiles.Budgeting,
        GateIRelationshipKind.ScenarioVersion or GateIRelationshipKind.ComparatorOutput
            or GateIRelationshipKind.SelectedScenario => S2SOutboundReceiverProfiles.ScenarioPlanning,
        GateIRelationshipKind.BenefitOutcome => S2SOutboundReceiverProfiles.OutcomeTracking,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}
