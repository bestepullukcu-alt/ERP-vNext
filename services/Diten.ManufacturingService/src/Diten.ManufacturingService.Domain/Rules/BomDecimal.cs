using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.ManufacturingService.Domain.Rules;

/// <summary>
/// Contract <c>Decimal</c> (<c>^-?\d+(\.\d+)?$</c>, string) ile <see cref="decimal"/> arasındaki tek dönüşüm noktası.
/// BOM miktarları negatif olamaz, bu yüzden kabul edilen biçim <c>^\d+(\.\d+)?$</c>'dir. float/double hiçbir yerde yok.
/// </summary>
public static partial class BomDecimal
{
    public const int MaxFractionDigits = 18;

    [GeneratedRegex(@"^\d{1,12}(\.\d{1,18})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    /// <summary>Pozitif (> 0) decimal string mi.</summary>
    public static bool IsPositive(string? text) =>
        text is not null && Shape().IsMatch(text) && TryParse(text, out var value) && value > 0m;

    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;
        return text is not null && Shape().IsMatch(text)
            && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    public static int Scale(string text)
    {
        var dot = text.IndexOf('.');
        return dot < 0 ? 0 : text.Length - dot - 1;
    }

    /// <summary>
    /// Çarpım, girdilerin büyük ölçeğiyle yazılır (frozen örnek: "100.000" × "2.000" = "200.000"). O ölçekte yuvarlama
    /// değeri değiştirecekse tam ölçek korunur — miktar asla sessizce kırpılmaz.
    /// </summary>
    public static string Multiply(string left, string right)
    {
        var product = decimal.Parse(left, CultureInfo.InvariantCulture) * decimal.Parse(right, CultureInfo.InvariantCulture);
        return Format(product, Math.Max(Scale(left), Scale(right)));
    }

    public static string Add(string left, string right)
    {
        var sum = decimal.Parse(left, CultureInfo.InvariantCulture) + decimal.Parse(right, CultureInfo.InvariantCulture);
        return Format(sum, Math.Max(Scale(left), Scale(right)));
    }

    private static string Format(decimal value, int scale)
    {
        // decimal.ToString() keeps the product's own scale (e.g. 0.333 × 0.333 = 0.110889), so the fallback loses nothing.
        return Math.Round(value, scale) == value
            ? value.ToString("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
    }
}
