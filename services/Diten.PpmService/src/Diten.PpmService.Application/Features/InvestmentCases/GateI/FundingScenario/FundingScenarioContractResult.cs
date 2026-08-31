using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public sealed record FundingScenarioContractResult(int HttpStatus,string StableCode,bool ContractSatisfied)
{ public bool IsExecutable=>false; }
