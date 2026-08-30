using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public static class FundingScenarioFailureCodes
{
    public const string InvalidRequest="ppm_gate_ib_invalid_request",AuthenticationRequired="ppm_gate_ib_authentication_required",Forbidden="ppm_gate_ib_forbidden",NotFound="ppm_gate_ib_not_found",Conflict="ppm_gate_ib_conflict",ProviderUnavailable="ppm_gate_ib_provider_unavailable",RuntimeUnavailable="ppm_gate_ib_runtime_unavailable";
}
