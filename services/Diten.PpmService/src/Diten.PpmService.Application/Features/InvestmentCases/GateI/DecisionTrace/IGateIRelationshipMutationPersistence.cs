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


public interface IGateIRelationshipMutationPersistence
{
    Task<GateIReceiptResult> ReconcileAsync(
        GateIMutationScope scope,
        CancellationToken cancellationToken);

    Task<GateIRelationshipMutationResult> ExecuteInvestmentCaseAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        Action<InvestmentCase> mutation,
        string mutationName,
        CancellationToken cancellationToken);

    Task<GateIRelationshipMutationResult> ExecuteBenefitCommitmentAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        Action<BenefitCommitment> mutation,
        string mutationName,
        CancellationToken cancellationToken);
}
