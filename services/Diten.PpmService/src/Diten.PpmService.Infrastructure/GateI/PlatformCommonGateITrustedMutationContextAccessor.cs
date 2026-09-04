using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;

internal sealed class PlatformCommonGateITrustedMutationContextAccessor(
    IS2STrustedRequestContextAccessor accessor) : IGateITrustedMutationContextAccessor
{
    public GateITrustedMutationContext? Current
    {
        get
        {
            S2STrustedRequestContext trusted;
            try
            {
                trusted = accessor.Current?.Validate()
                    ?? throw new ArgumentException("S2S trusted context is unavailable.");
            }
            catch (ArgumentException)
            {
                return null;
            }

            return new GateITrustedMutationContext(
                trusted.TenantId,
                trusted.EffectiveActorId,
                trusted.DelegatedActorId,
                trusted.DelegationId,
                trusted.ServicePrincipalId,
                trusted.CredentialId,
                trusted.ClientId,
                trusted.Issuer,
                trusted.Audience,
                trusted.TokenType,
                trusted.ProtocolScope,
                trusted.OperationId,
                trusted.Permissions,
                trusted.RequestHash,
                trusted.TenantGrantVersion,
                trusted.ServicePrincipalVersion,
                trusted.CredentialGeneration);
        }
    }
}
