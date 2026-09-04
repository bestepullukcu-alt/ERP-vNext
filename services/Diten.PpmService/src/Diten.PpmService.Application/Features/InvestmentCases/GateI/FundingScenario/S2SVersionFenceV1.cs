using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public sealed record S2SVersionFenceV1(long PrincipalVersion,long CredentialGeneration,long AuthorizationVersion,string EntitlementVersion);
