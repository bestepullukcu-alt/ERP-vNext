using Diten.InventoryService.Application.Features.CountEvidenceExports;

namespace Diten.InventoryService.Application.Features.CycleCounting.Ports;

/// <summary>
/// DL-5 (pack §4.5) — draws a <see cref="CountEvidenceDocument"/> as PDF bytes (PDFsharp / MigraDoc, the MOD-0176 renderer pattern). The handler
/// hashes the returned bytes; the renderer adds no data of its own (the sections are <see cref="CountEvidenceSections"/>, shared with the CSV).
/// </summary>
public interface ICycleCountEvidencePdfRenderer
{
    byte[] Render(CountEvidenceDocument document);
}
