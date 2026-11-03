namespace Diten.Platform.Application.Features.Lookups.Services;

/// <summary>
/// MOD-0048-FU01 §21 — the published <c>currency-minor-units</c> set was found neither in the caller's tenant nor in the
/// reference tenant. The picker has no answer: the API's exception handler turns this into 503 (reason
/// <see cref="ReasonCode"/>). Never answered with the server's ICU list or an empty list.
/// </summary>
public sealed class CurrencySetUnavailableException : Exception
{
    /// <summary>The stable reason code of the 503 answer.</summary>
    public const string ReasonCode = "currency_set_unavailable";

    public CurrencySetUnavailableException()
        : base("The published currency list could not be read.")
    {
    }
}
