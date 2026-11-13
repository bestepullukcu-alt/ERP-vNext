using Diten.CrmService.Application.Features.VisitEvidence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Common;

/// <summary>
/// WP-VW-W6 W6-BE-c1 (user decision B, 2026-10-21) — the language of an e-mail to a recipient who has no language of their
/// own: a mailbox (W5 PV / MSL), a depot, a pharmacy. It is the language of the SELLING COMPANY's country (the legal
/// entity's MDM <c>CountryCode</c>) when that country speaks one of the seven supported languages; otherwise null, which
/// the Platform's internal dispatch answers with the tenant's own language. Internal users (rep, manager) are never
/// asked here — they get their own preference. ONE rule for W5 and W6.
/// </summary>
public static class ExternalRecipientLanguages
{
    /// <summary>The Arabic-speaking countries (ISO 3166-1 alpha-2) — the Arab League members.</summary>
    public static readonly IReadOnlySet<string> ArabicCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AE", "BH", "DJ", "DZ", "EG", "IQ", "JO", "KM", "KW", "LB", "LY", "MA", "MR", "OM", "PS", "QA", "SA", "SD", "SO", "SY", "TN", "YE"
    };

    /// <summary>The countries whose business language is Russian besides Russia itself (user decision 2026-10-21, W6 E4:
    /// the CIS / post-Soviet countries of the tenant's companies).</summary>
    public static readonly IReadOnlySet<string> RussianCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RU", "KZ", "KG", "BY", "UZ", "TJ", "TM", "AZ", "GE", "UA", "MD"
    };

    /// <summary>The language for a country code, or null (= the tenant's language).</summary>
    public static string? ForCountry(string? countryCode)
    {
        var code = countryCode?.Trim().ToUpperInvariant();
        return code switch
        {
            null or "" => null,
            "TR" => "tr",
            "FR" => "fr",
            "ES" => "es",
            "CN" => "zh",
            _ when RussianCountries.Contains(code) => "ru",
            _ when ArabicCountries.Contains(code) => "ar",
            _ => null
        };
    }
}

/// <summary>A legal entity's country as MDM holds it; null = unknown or MDM could not be asked.</summary>
public interface ILegalEntityCountryReader
{
    Task<string?> CountryOfAsync(Guid legalEntityId, CancellationToken ct);
}

/// <summary>The language of an external recipient for a mail a USER's company sends (W5: the rep's primary seat → legal
/// entity → MDM country). Never throws: anything that cannot be read is null (= the tenant's language).</summary>
public interface IExternalRecipientLanguageResolver
{
    Task<string?> ForUserAsync(string? userId, CancellationToken ct);
}

public sealed class ExternalRecipientLanguageResolver : IExternalRecipientLanguageResolver
{
    private readonly IOrganizationSeatReader _seats;
    private readonly ILegalEntityCountryReader _countries;
    private readonly ILogger<ExternalRecipientLanguageResolver> _logger;

    public ExternalRecipientLanguageResolver(IOrganizationSeatReader seats, ILegalEntityCountryReader countries,
        ILogger<ExternalRecipientLanguageResolver>? logger = null)
    {
        _seats = seats;
        _countries = countries;
        _logger = logger ?? NullLogger<ExternalRecipientLanguageResolver>.Instance;
    }

    public async Task<string?> ForUserAsync(string? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        try
        {
            var seat = await _seats.GetAsync(userId, ct);
            if (!seat.Available || seat.Primary?.LegalEntityId is not { } legalEntityId || legalEntityId == Guid.Empty)
            {
                return null;
            }

            return ExternalRecipientLanguages.ForCountry(await _countries.CountryOfAsync(legalEntityId, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "external_language.unresolved — the tenant's language applies.");
            return null;
        }
    }
}
