namespace Diten.Platform.Application.Features.PayrollSources;

public static class PayrollSourceCodeNormalizer
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
