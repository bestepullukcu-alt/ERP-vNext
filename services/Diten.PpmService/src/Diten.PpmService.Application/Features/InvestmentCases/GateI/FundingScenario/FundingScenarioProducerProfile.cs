using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public sealed record FundingScenarioProducerProfile(
    string OwnerModule,string OperationId,string Permission,string Audience,string ClientId,string ProtocolScope,
    string FixtureClosure,string CoreCheckpoint,string SigningIdentity,string FixtureKeyId,string SigningVectorPath);
