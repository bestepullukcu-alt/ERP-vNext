using PdfSharp.Fonts;
using PdfSharp.WPFonts;

namespace Diten.CrmService.Infrastructure.ContentComposition.Rendering;

/// <summary>
/// The one font family of the CRM PDFs, served from the package's embedded Segoe WP bytes (Latin incl. Turkish), so the
/// output is the same on every OS and never depends on installed fonts. Installed once per process; if another component
/// already installed a resolver, that one is left alone and this one becomes the fallback. There is no italic face:
/// an italic request resolves to the upright face of the same weight.
/// </summary>
public sealed class CrmPdfFontResolver : IFontResolver
{
    public const string FamilyName = "DitenCrmSans";

    private const string Regular = "DitenCrmSans-Regular";
    private const string Bold = "DitenCrmSans-Bold";
    private static readonly object Gate = new();
    private static bool _installed;

    public static void EnsureInstalled()
    {
        if (_installed)
        {
            return;
        }

        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new CrmPdfFontResolver();
            }
            else
            {
                GlobalFontSettings.FallbackFontResolver ??= new CrmPdfFontResolver();
            }

            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase)
            ? new FontResolverInfo(bold ? Bold : Regular)
            : null;

    public byte[]? GetFont(string faceName) => faceName switch
    {
        Regular => FontDataHelper.SegoeWP,
        Bold => FontDataHelper.SegoeWPBold,
        _ => null
    };
}
