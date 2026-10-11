using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Features.Knowledge.Path.Release;
using MigraDoc.DocumentObjectModel;

namespace Diten.CrmService.Infrastructure.ContentComposition.Rendering;

/// <summary>
/// WP-KP-3 (DESIGN-KP-STUDIO §3.4, first phase) — the archive PDF of an approved knowledge path revision, with
/// PDFsharp/MigraDoc (the SCMM-16B engine and helpers): title + approval code, context (chain, country, language,
/// product, audience), the chain steps in branch-first order with their contents (title + version, field order) and
/// claims (code + the country version's text and qualifier in the path language), the chain conformance, the MLR round
/// (step name, decision, person, time, comment) and the generation time. Bytes in, bytes out.
/// </summary>
public sealed class PdfSharpKnowledgePathRevisionRenderer : IKnowledgePathRevisionRenderer
{
    public RenderedContent Render(KnowledgePathRenderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var (document, section) = MigraDocPdf.Create(model.ApprovalCode, "Knowledge path revision (approved, archive copy)");
        MigraDocPdf.AddTitle(section, model.PathName);
        MigraDocPdf.AddField(section, "Approval code", model.ApprovalCode);
        MigraDocPdf.AddField(section, "Path", $"{model.PathCode} v{model.PathVersion} · Rev {model.RevisionNumber}");
        MigraDocPdf.AddField(section, "Submitted by", model.SubmittedBy);

        MigraDocPdf.AddHeading(section, "Context");
        MigraDocPdf.AddField(section, "Chain", model.ChainName is null ? null : $"{model.ChainName} v{model.ChainVersion}");
        MigraDocPdf.AddField(section, "Country", model.CountryName);
        MigraDocPdf.AddField(section, "Language", model.LanguageName);
        MigraDocPdf.AddField(section, "Product", model.ProductName);
        MigraDocPdf.AddField(section, "Audience", model.Audiences.Count == 0 ? null : string.Join(", ", model.Audiences));

        MigraDocPdf.AddHeading(section, "Steps (field order)");
        foreach (var slot in model.Slots)
        {
            var heading = section.AddParagraph();
            heading.Format.SpaceBefore = "6pt";
            heading.AddFormattedText($"{slot.BranchLabel} › {slot.SlotLabel}", TextFormat.Bold);
            if (slot.Contents.Count == 0 && slot.Claims.Count == 0)
            {
                section.AddParagraph("No item.");
            }

            foreach (var content in slot.Contents)
            {
                section.AddParagraph($"{content.Order}. {content.Title} — {content.ContentCode} v{content.ContentVersion}"
                                     + (content.IsRequired ? " (required)" : string.Empty));
            }

            foreach (var claim in slot.Claims)
            {
                var p = section.AddParagraph();
                p.Format.LeftIndent = "12pt";
                p.AddFormattedText($"{claim.ClaimCode}", TextFormat.Bold);
                p.AddText($" — “{MigraDocPdf.DisplayText(claim.Text)}”");
                if (!string.IsNullOrWhiteSpace(claim.Qualifier))
                {
                    p.AddText($" ({claim.Qualifier.Trim()})");
                }

                p.AddText($" · v{MigraDocPdf.DisplayText(claim.CountryVersion)}");
            }
        }

        MigraDocPdf.AddHeading(section, "Chain conformance");
        foreach (var slot in model.Slots)
        {
            section.AddParagraph($"• {slot.BranchLabel} › {slot.SlotLabel}: {slot.Count} (min {slot.Min}, max {slot.Max?.ToString() ?? "∞"}) — {slot.Status}");
        }

        MigraDocPdf.AddHeading(section, "MLR review");
        if (model.Review.Count == 0)
        {
            section.AddParagraph("No review history.");
        }

        foreach (var entry in model.Review)
        {
            var p = section.AddParagraph($"• {entry.At:u} — {MigraDocPdf.DisplayText(entry.StepName)}: {entry.Action} by {MigraDocPdf.DisplayText(entry.Actor)}");
            if (!string.IsNullOrWhiteSpace(entry.Comment))
            {
                p.AddText($" — “{entry.Comment.Trim()}”");
            }
        }

        MigraDocPdf.AddHeading(section, "Generated");
        MigraDocPdf.AddField(section, "At (UTC)", model.GeneratedAt.ToString("u"));
        MigraDocPdf.AddField(section, "By", model.GeneratedBy);

        var fileName = $"{MigraDocPdf.Sanitize(model.ApprovalCode, "knowledge-path-revision")}.pdf";
        return new RenderedContent(MigraDocPdf.ToBytes(document), fileName, "application/pdf");
    }
}
