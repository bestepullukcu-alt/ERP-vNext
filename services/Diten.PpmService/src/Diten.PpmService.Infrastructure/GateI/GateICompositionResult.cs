using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;


public sealed record GateICompositionResult(
    int StatusCode,
    string StableCode,
    int ProviderCalls,
    int RelationshipWrites,
    int ReceiptWrites,
    int AuditIntentWrites,
    int OutboxWrites);
