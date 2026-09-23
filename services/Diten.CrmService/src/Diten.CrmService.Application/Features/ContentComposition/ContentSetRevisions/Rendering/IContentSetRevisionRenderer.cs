using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — renders an approved <see cref="ContentSetRevision"/>'s frozen manifest into a
/// single output document (one PDF). The render is <b>deterministic over the frozen snapshot</b>: the same revision always
/// produces the same logical document, which is what makes the render idempotent (AT05) at the handler. The seam lives in
/// Application so the handler depends only on it; the concrete PDF engine (PDFsharp/MigraDoc) is an Infrastructure detail.
/// </summary>
public interface IContentSetRevisionRenderer
{
    /// <summary>Render the revision to bytes. Implementations must return a valid document (a PDF begins with the
    /// <c>%PDF</c> magic bytes). No I/O, no storage — producing bytes is all this does.</summary>
    RenderedContent Render(ContentSetRevision revision);
}

/// <summary>The rendered output: the bytes plus the file name and media type to store them under.</summary>
public sealed record RenderedContent(byte[] Bytes, string FileName, string MediaType);
