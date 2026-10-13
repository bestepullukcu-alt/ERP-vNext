using System.Text.RegularExpressions;

namespace Diten.InventoryService.Domain.Quarantine.Rules;

/// <summary>
/// CT-Q8: the local tenant block-reason list starts with the five reasons of the approved mockup (<c>kar.app.js:33</c>):
/// Kalite ret, Sapma, Hasar, SKT, Diğer — names in the seven tenant languages (data, pack §11.6). V20: code
/// <c>^[A-Z][A-Z0-9_]{1,31}$</c>, immutable; a used code cannot be deactivated.
/// </summary>
public static partial class BlockReasonSeeds
{
    public static readonly IReadOnlyList<(string Code, IReadOnlyDictionary<string, string> Names)> All =
    [
        ("QUALITY_REJECT", Names("Quality rejection", "Kalite ret", "Rejet qualité", "Rechazo de calidad", "质量拒收", "رفض الجودة", "Отказ по качеству")),
        ("DEVIATION", Names("Deviation", "Sapma", "Déviation", "Desviación", "偏差", "انحراف", "Отклонение")),
        ("DAMAGE", Names("Damage", "Hasar", "Dommage", "Daño", "损坏", "تلف", "Повреждение")),
        ("EXPIRY", Names("Expiry", "SKT", "Péremption", "Caducidad", "过期", "انتهاء الصلاحية", "Истечение срока годности")),
        ("OTHER", Names("Other", "Diğer", "Autre", "Otro", "其他", "أخرى", "Другое"))
    ];

    public static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code);

    private static IReadOnlyDictionary<string, string> Names(string en, string tr, string fr, string es, string zh, string ar, string ru)
        => new Dictionary<string, string> { ["en"] = en, ["tr"] = tr, ["fr"] = fr, ["es"] = es, ["zh"] = zh, ["ar"] = ar, ["ru"] = ru };

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
