using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public interface IScenarioPlanningReferenceValidationPort
{ ValueTask<ProducerReferenceValidationResult> ValidateAsync(ScenarioReferenceValidationRequest request,S2SFundingScenarioContextV1 context,CancellationToken cancellationToken); }
