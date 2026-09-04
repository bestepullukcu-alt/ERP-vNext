using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Infrastructure.GateI;


public sealed record GateIOwnerReferenceLocalEvidencePorts(
    IDecisionReferenceValidationPort DecisionTrace,
    IBudgetVersionReferenceValidationPort Budgeting,
    IScenarioPlanningReferenceValidationPort ScenarioPlanning,
    IOutcomeReferenceAuthorityPort OutcomeTracking,
    IGateIRelationshipAuthority RelationshipAuthority);
