using System.Globalization;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.CompetitorProducts;

/// <summary>WP-VW-W4 (W4-BE-b2) — the coded failures of the competitor products (<c>[code, message]</c>).</summary>
public static class CompetitorErrors
{
    public const string Invalid = "competitor_invalid";
    public const string Duplicate = "competitor_duplicate";
    public const string MarketDuplicate = "competitor_market_duplicate";
    public const string MarketInvalid = "competitor_market_invalid";
    public const string ReimbursementInvalid = "competitor_reimbursement_invalid";
    public const string CurrencyInvalid = "competitor_currency_invalid";
    public const string FormInvalid = "competitor_form_invalid";
    public const string NotFound = "competitor_not_found";
    public const string NotEditable = "competitor_not_editable";
    public const string NotSelectable = "competitor_not_selectable";
    public const string NotProposal = "competitor_not_proposal";
    public const string DecisionLocked = "competitor_decision_locked";
    public const string DecisionMissing = "competitor_decision_missing";
    public const string Sod = "competitor_proposal_sod";
    public const string MergeConfirmRequired = "competitor_merge_confirm_required";
    public const string MergeSelf = "competitor_merge_self";
    public const string ReferenceDataUnavailable = "reference_data_unavailable";
    public const string ConcurrencyConflict = "concurrency_conflict";

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);
}

public static class CompetitorLimits
{
    public const int Name = 200;
    public const int Company = 200;
    public const int Alias = 200;
    public const int MaxAliases = 30;
    public const int Ingredient = 120;
    public const int MaxIngredients = 10;
    public const int Strength = 60;
    public const int Atc = 10;
    public const int Source = 300;
    public const int Reason = 500;
    public const int MaxProducts = 20;
    public const int MaxIndications = 30;
    public const int MaxMarkets = 60;

    /// <summary>"Possibly the same record" (G5).</summary>
    public const int LikelySameScore = 80;
}

/// <summary>WP-VW-W4 (W4-BE-b2) — the BRD set codes the record validates against.</summary>
public static class CompetitorReferenceSets
{
    /// <summary>The tenant set of reimbursement statuses (K-W6; the tenant adapts it).</summary>
    public const string ReimbursementStatus = "mdm-reimbursement-status";
}

/// <summary>
/// WP-VW-W4 (W4-BE-b2, G5) — name normalization and similarity. Lower case (invariant) + Turkish folding (ı / İ → i,
/// ş → s, ğ → g, ü → u, ö → o, ç → c, every other accent stripped) + punctuation / spaces collapsed; the similarity is the
/// Dice coefficient of the padded character trigrams, 0–100, the best of the name and the aliases.
/// </summary>
public static class CompetitorNames
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(ch switch
            {
                'ı' or 'İ' or 'I' => 'i',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ü' or 'Ü' => 'u',
                'ö' or 'Ö' => 'o',
                'ç' or 'Ç' => 'c',
                _ => char.ToLowerInvariant(ch)
            });
        }

        var decomposed = builder.ToString().Normalize(NormalizationForm.FormD);
        var clean = new StringBuilder(decomposed.Length);
        var space = false;
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch))
            {
                clean.Append(ch);
                space = false;
            }
            else if (!space && clean.Length > 0)
            {
                clean.Append(' ');
                space = true;
            }
        }

        return clean.ToString().Trim();
    }

    /// <summary>The tenant-unique key: folded company + folded name, letters / digits only.</summary>
    public static string Key(string? company, string? name)
        => $"{Fold(company).Replace(" ", string.Empty, StringComparison.Ordinal)}|{Fold(name).Replace(" ", string.Empty, StringComparison.Ordinal)}";

    public static int Similarity(string? a, string? b)
    {
        var x = Fold(a);
        var y = Fold(b);
        if (x.Length == 0 || y.Length == 0) return 0;
        if (x == y) return 100;
        var gx = Trigrams(x);
        var gy = Trigrams(y);
        var common = 0;
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in gy) pool[g] = pool.GetValueOrDefault(g) + 1;
        foreach (var g in gx)
        {
            if (pool.TryGetValue(g, out var n) && n > 0)
            {
                common++;
                pool[g] = n - 1;
            }
        }

        return (int)Math.Round(200.0 * common / (gx.Count + gy.Count), MidpointRounding.AwayFromZero);
    }

    /// <summary>The best similarity of <paramref name="name"/> against a record's name and aliases.</summary>
    public static int BestSimilarity(string? name, CompetitorProduct record)
        => record.Aliases.Prepend(record.Name).Select(n => Similarity(name, n)).DefaultIfEmpty(0).Max();

    private static List<string> Trigrams(string value)
    {
        var padded = $"  {value} ";
        var grams = new List<string>(padded.Length);
        for (var i = 0; i + 3 <= padded.Length; i++) grams.Add(padded.Substring(i, 3));
        return grams;
    }
}

/// <summary>WP-VW-W4 (W4-BE-b2) — the shape checks of a write (the reference checks are the service's).</summary>
public static class CompetitorValidation
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Distinct, trimmed, non-empty; the order of first appearance is kept.</summary>
    public static List<string> Tags(IEnumerable<string>? values)
    {
        var list = new List<string>();
        foreach (var v in values ?? [])
        {
            var t = Clean(v);
            if (t is not null && !list.Contains(t, StringComparer.OrdinalIgnoreCase)) list.Add(t);
        }

        return list;
    }

    /// <summary>The checks of a complete record (create / update / the "complete" decision). Null = valid.</summary>
    public static (string Code, string Message)? Check(CompetitorProductFields f)
    {
        if (Clean(f.Name) is not { } name || name.Length > CompetitorLimits.Name)
            return (CompetitorErrors.Invalid, $"The name is required (≤ {CompetitorLimits.Name}).");
        if (Clean(f.Company) is not { } company || company.Length > CompetitorLimits.Company)
            return (CompetitorErrors.Invalid, $"The company is required (≤ {CompetitorLimits.Company}).");
        if (f.ActiveIngredients.Count == 0 || f.ActiveIngredients.Count > CompetitorLimits.MaxIngredients
            || f.ActiveIngredients.Any(i => i.Length > CompetitorLimits.Ingredient))
            return (CompetitorErrors.Invalid, $"At least one and at most {CompetitorLimits.MaxIngredients} active ingredients (each ≤ {CompetitorLimits.Ingredient}).");
        if (f.Aliases.Count > CompetitorLimits.MaxAliases || f.Aliases.Any(a => a.Length > CompetitorLimits.Alias))
            return (CompetitorErrors.Invalid, $"At most {CompetitorLimits.MaxAliases} other names (each ≤ {CompetitorLimits.Alias}).");
        if (f.FormCode is not null && !BrandProductVocabulary.IsDosageForm(f.FormCode))
            return (CompetitorErrors.FormInvalid, $"'{f.FormCode}' is not an MDM dosage form.");
        if (f.Strength is { Length: > CompetitorLimits.Strength } || f.AtcCode is { Length: > CompetitorLimits.Atc })
            return (CompetitorErrors.Invalid, $"Strength ≤ {CompetitorLimits.Strength}, ATC code ≤ {CompetitorLimits.Atc}.");
        if (f.CompetesWithGlobalProductIds.Count == 0 || f.CompetesWithGlobalProductIds.Count > CompetitorLimits.MaxProducts
            || f.CompetesWithGlobalProductIds.Contains(Guid.Empty))
            return (CompetitorErrors.Invalid, $"At least one and at most {CompetitorLimits.MaxProducts} of our products.");
        if (f.IndicationNodeIds.Count > CompetitorLimits.MaxIndications || f.IndicationNodeIds.Contains(Guid.Empty))
            return (CompetitorErrors.Invalid, $"At most {CompetitorLimits.MaxIndications} indications.");
        if (f.CountryPrices.Count > CompetitorLimits.MaxMarkets)
            return (CompetitorErrors.Invalid, $"At most {CompetitorLimits.MaxMarkets} markets.");

        var markets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in f.CountryPrices)
        {
            if (string.IsNullOrWhiteSpace(row.MarketCode))
                return (CompetitorErrors.MarketInvalid, "Every price row needs a market.");
            if (!markets.Add(row.MarketCode.Trim()))
                return (CompetitorErrors.MarketDuplicate, $"The market '{row.MarketCode}' appears twice; a price change is a new period of the same row.");
            if (row.BoxPrice < 0 || row.BoxPrice > 100_000_000m)
                return (CompetitorErrors.Invalid, "The box price must be between 0 and 100,000,000.");
            if (row.CurrencyCode is not { Length: 3 } currency || !currency.All(char.IsAsciiLetterUpper))
                return (CompetitorErrors.CurrencyInvalid, $"'{row.CurrencyCode}' is not a three-letter currency code.");
            if (string.IsNullOrWhiteSpace(row.ReimbursementStatusCode))
                return (CompetitorErrors.ReimbursementInvalid, "Every price row needs a reimbursement status.");
            if (row.Source is { Length: > CompetitorLimits.Source })
                return (CompetitorErrors.Invalid, $"The source is at most {CompetitorLimits.Source} characters.");
        }

        return null;
    }

    /// <summary>The normalized copy of a write (trimmed text, distinct tags / ids, upper-case market and currency codes,
    /// UTC calendar day).</summary>
    public static CompetitorProductFields Normalize(CompetitorProductFields f) => new()
    {
        Name = Clean(f.Name) ?? string.Empty,
        Company = Clean(f.Company) ?? string.Empty,
        Aliases = Tags(f.Aliases),
        ActiveIngredients = Tags(f.ActiveIngredients),
        FormCode = Clean(f.FormCode)?.ToLowerInvariant(),
        Strength = Clean(f.Strength),
        AtcCode = Clean(f.AtcCode)?.ToUpperInvariant(),
        CompetesWithGlobalProductIds = (f.CompetesWithGlobalProductIds ?? []).Distinct().ToList(),
        IndicationNodeIds = (f.IndicationNodeIds ?? []).Distinct().ToList(),
        CountryPrices = (f.CountryPrices ?? []).Select(r => new CompetitorCountryPrice
        {
            MarketCode = (r.MarketCode ?? string.Empty).Trim().ToUpperInvariant(),
            BoxPrice = decimal.Round(r.BoxPrice, 4),
            CurrencyCode = (r.CurrencyCode ?? string.Empty).Trim().ToUpperInvariant(),
            ReimbursementStatusCode = (r.ReimbursementStatusCode ?? string.Empty).Trim(),
            ValidFrom = DateTime.SpecifyKind(r.ValidFrom == default ? DateTime.UtcNow.Date : r.ValidFrom.Date, DateTimeKind.Utc),
            Source = Clean(r.Source)
        }).ToList()
    };
}
