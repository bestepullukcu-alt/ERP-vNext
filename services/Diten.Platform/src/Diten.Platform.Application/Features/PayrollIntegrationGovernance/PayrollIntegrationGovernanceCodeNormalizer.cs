namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance;

public static class PayrollIntegrationGovernanceCodeNormalizer
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
