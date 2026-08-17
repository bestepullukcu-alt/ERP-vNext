namespace Diten.Platform.Application.Features.Billing;

public static class BillingMoney
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string NormalizeCurrency(string currency) => currency.Trim().ToUpperInvariant();
}
