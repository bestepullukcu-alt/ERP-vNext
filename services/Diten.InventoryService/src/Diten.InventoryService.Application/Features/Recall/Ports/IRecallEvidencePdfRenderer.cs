using Diten.InventoryService.Application.Features.Recall.Evidence;

namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>
/// WP-0177-R7b — draws a <see cref="RecallEvidenceDocument"/> as PDF bytes (PDFsharp-MigraDoc 6.1.1, MIT — CT-R14; the 0176 F5 renderer's
/// embedded font). The handler hashes the returned bytes; the renderer adds no data of its own.
/// </summary>
public interface IRecallEvidencePdfRenderer
{
    byte[] Render(RecallEvidenceDocument document);
}
