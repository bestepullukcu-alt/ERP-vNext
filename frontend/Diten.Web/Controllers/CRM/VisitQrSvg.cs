using QRCoder;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-WEB-a, D-9) — the institution QR code drawn on the Web server as an SVG (QRCoder 1.6.0, MIT; no CDN, no
/// client library). Error correction M (a sticker on a reception desk gets scuffed), a quiet zone of 4 modules, black on
/// white, scalable (the page sizes it). The payload is drawn, never logged or echoed in text.
/// </summary>
public static class VisitQrSvg
{
    public static string Render(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data);
        return svg.GetGraphic(new System.Drawing.Size(256, 256), "#000000", "#ffffff", drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
