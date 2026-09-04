using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public enum ProducerReferenceState { Allowed, MissingOrInvisible, IneligibleOrStale, Unavailable, Malformed, Indeterminate, UnsupportedVersion }
