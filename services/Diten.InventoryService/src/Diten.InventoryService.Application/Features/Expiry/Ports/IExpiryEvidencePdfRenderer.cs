namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// WP-0176-F5 — draws an <see cref="ExpiryEvidenceDocument"/> as PDF bytes (PDFsharp / MigraDoc 6.1.1, MIT — CT-R14; Turkish characters
/// through an embedded font). The handler hashes the returned bytes; the renderer adds no data of its own.
/// </summary>
public interface IExpiryEvidencePdfRenderer
{
    byte[] Render(ExpiryEvidenceDocument document);
}
