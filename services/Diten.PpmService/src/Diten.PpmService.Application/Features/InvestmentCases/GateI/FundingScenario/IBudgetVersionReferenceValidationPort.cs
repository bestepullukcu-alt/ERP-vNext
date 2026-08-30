using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public interface IBudgetVersionReferenceValidationPort
{ ValueTask<ProducerReferenceValidationResult> ValidateAsync(BudgetReferenceValidationRequest request,S2SFundingScenarioContextV1 context,CancellationToken cancellationToken); }
