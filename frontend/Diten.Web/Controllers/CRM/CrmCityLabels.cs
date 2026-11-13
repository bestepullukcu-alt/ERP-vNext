using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-FIX, G-3) — an institution's city in words. The account carries <c>TR-34-ISTANBUL</c> (the CRM's city
/// reference) while the MOD-0048 <c>city</c> set is keyed <c>istanbul</c> — the known dictionary divergence (a data-owner
/// item). The label is read in this order: the set's label for the code as it is; the set's label for the NAME part of a
/// <c>CC-NN-NAME</c> code (<c>istanbul</c> → "İstanbul"); else a readable name made from that part ("Konya"). Turkish
/// letters in the code are cased with Turkish rules (<c>İSTANBUL</c> → "İstanbul", never "i̇stanbul"); a plain ASCII
/// code is cased invariantly. Any other shape is shown as it came. The JS twin is <c>VisitQrCore.cityLabel</c>.
/// </summary>
public static partial class CrmCityLabels
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string? Resolve(string? code, IReadOnlyDictionary<string, string>? labels)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim();
        if (labels is not null && labels.TryGetValue(c, out var direct) && !string.IsNullOrWhiteSpace(direct)) return direct;
        var m = CodeShape().Match(c);
        if (!m.Success) return c;
        var name = m.Groups["name"].Value;
        if (labels is not null)
        {
            foreach (var key in new[] { name, Fold(name), name.Replace('-', ' '), Fold(name).Replace("-", string.Empty) })
            {
                if (labels.TryGetValue(key, out var byName) && !string.IsNullOrWhiteSpace(byName)) return byName;
            }
        }

        return Readable(name);
    }

    /// <summary>"KAHRAMANMARAS" → "Kahramanmaras", "İSTANBUL" → "İstanbul", "YENI-MAHALLE" → "Yeni Mahalle".</summary>
    public static string Readable(string name)
    {
        var culture = TurkishLetters().IsMatch(name) ? Turkish : CultureInfo.InvariantCulture;
        var words = name.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLower(culture))
            .Select(w => w.Length == 0 ? w : w[..1].ToUpper(culture) + w[1..]);
        return string.Join(' ', words);
    }

    /// <summary>The set's ASCII key of a name ("İSTANBUL" → "istanbul", "AĞRI" → "agri").</summary>
    private static string Fold(string name)
    {
        var lower = name.ToLower(Turkish);
        var map = new Dictionary<char, char> { ['ı'] = 'i', ['ğ'] = 'g', ['ş'] = 's', ['ç'] = 'c', ['ö'] = 'o', ['ü'] = 'u', ['â'] = 'a', ['î'] = 'i', ['û'] = 'u' };
        return new string(lower.Select(ch => map.GetValueOrDefault(ch, ch)).ToArray());
    }

    [GeneratedRegex(@"^[A-Za-z]{2}-\d{1,3}-(?<name>.+)$")]
    private static partial Regex CodeShape();

    [GeneratedRegex("[İıĞğŞşÇçÖöÜü]")]
    private static partial Regex TurkishLetters();
}
