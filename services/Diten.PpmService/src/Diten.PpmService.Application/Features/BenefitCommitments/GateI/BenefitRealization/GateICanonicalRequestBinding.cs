using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;


public static class GateICanonicalRequestBinding
{
    public static bool IsWellFormed(string method, string path, string hash) =>
        !string.IsNullOrEmpty(method) && string.Equals(method, method.ToUpperInvariant(), StringComparison.Ordinal) &&
        !string.IsNullOrEmpty(path) && path.StartsWith("/", StringComparison.Ordinal) && !path.Contains('?', StringComparison.Ordinal) &&
        S2SOutboundCanonicalRequestBinding.IsLowerHex64(hash);

    public static string Compute(string method, string path, Guid tenantId, string operation, ReadOnlySpan<byte> body)
    {
        return S2SOutboundCanonicalRequestBinding.Compute(
            method, path, body, tenantId, operation,
            [BenefitCommitmentOutcomeReferenceValidator.Permission]);
    }
}
