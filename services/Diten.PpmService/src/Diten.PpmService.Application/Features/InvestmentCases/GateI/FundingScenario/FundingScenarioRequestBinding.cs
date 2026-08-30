using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public static class FundingScenarioRequestBinding
{
    public static bool IsWellFormed(string method,string path,string hash)=>!string.IsNullOrEmpty(method)&&string.Equals(method,method.ToUpperInvariant(),StringComparison.Ordinal)&&!string.IsNullOrEmpty(path)&&path.StartsWith("/",StringComparison.Ordinal)&&!path.Contains('?',StringComparison.Ordinal)&&S2SOutboundCanonicalRequestBinding.IsLowerHex64(hash);
    public static string Compute(string method,string path,Guid tenantId,string operation,ReadOnlySpan<byte> body)
    {
        var profile=FundingScenarioAtomicLane.RequiredProfiles.SingleOrDefault(candidate=>string.Equals(candidate.OperationId,operation,StringComparison.Ordinal))??throw new ArgumentException("invalid_request_binding_input");
        return S2SOutboundCanonicalRequestBinding.Compute(method,path,body,tenantId,operation,[profile.Permission]);
    }
}
