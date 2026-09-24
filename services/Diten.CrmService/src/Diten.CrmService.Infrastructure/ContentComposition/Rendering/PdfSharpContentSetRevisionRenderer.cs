using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Diten.CrmService.Domain.Entities;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace Diten.CrmService.Infrastructure.ContentComposition.Rendering;

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — renders an approved <see cref="ContentSetRevision"/>'s frozen manifest into a
/// single PDF with PDFsharp/MigraDoc (MIT). The document is a deterministic projection of the snapshot only: identity
/// (RevisionCode/Number, ContentSet id + pinned version), the pinned Template, the Scope summary, the selected components
/// and claims, the eligibility snapshot, the tenant, and the generation time. No network, no I/O — bytes in, bytes out.
/// <para>
/// <b>Headless fonts.</b> On Windows (the current build/CI host) fonts resolve automatically via
/// <see cref="GlobalFontSettings.UseWindowsFontsUnderWindows"/>. On Linux/WSL a font resolver over a bundled open font is
/// the additive go-live step (F-SCMM-16B-FONT) — deliberately not shipped here to keep the change scope minimal, per the
/// WP. The renderer never falls back silently: if fonts cannot resolve, the render throws and the command fails.
/// </para>
/// </summary>
public sealed class PdfSharpContentSetRevisionRenderer : IContentSetRevisionRenderer
{
    private const string BodyFont = "Arial";

    public RenderedContent Render(ContentSetRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var document = BuildDocument(revision);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);

        var fileName = $"{Sanitize(revision.RevisionCode)}.pdf";
        return new RenderedContent(stream.ToArray(), fileName, "application/pdf");
    }

    private static Document BuildDocument(ContentSetRevision r)
    {
        var document = new Document();
        document.Info.Title = string.IsNullOrWhiteSpace(r.RevisionCode) ? "Content Set Revision" : r.RevisionCode;
        document.Info.Subject = "Content Set Revision (rendered artifact)";

        var normal = document.Styles["Normal"]!;
        normal.Font.Name = BodyFont;
        normal.Font.Size = 10;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;

        // ── header / identity ───────────────────────────────────────────────────────────────────────────────────────
        var title = section.AddParagraph($"Content Set Revision {DisplayText(r.RevisionCode)}");
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = "6pt";

        AddField(section, "Revision number", r.RevisionNumber.ToString());
        AddField(section, "Content set", $"{r.ContentSetId:D} (version {r.ContentSetVersion})");
        AddField(section, "Review status", r.ReviewStatus);
        AddField(section, "Tenant", r.TenantId.ToString("D"));
        AddField(section, "Rendered at (UTC)", DateTimeOffset.UtcNow.ToString("u"));
        if (r.Decision is { } d)
        {
            AddField(section, "Decision", $"{d.Decision} by {DisplayText(d.ReviewerId)} at {d.DecidedAt:u}");
        }

        // ── template ────────────────────────────────────────────────────────────────────────────────────────────────
        AddHeading(section, "Composition template");
        AddField(section, "Concept chain template", r.Template.ConceptChainTemplateId.ToString("D"));
        AddField(section, "Chain version", DisplayText(r.Template.ChainVersion));

        // ── scope ───────────────────────────────────────────────────────────────────────────────────────────────────
        AddHeading(section, "Scope");
        if (r.Scope is { } scope)
        {
            AddField(section, "Content scope", $"{scope.ContentScopeId:D} (version {DisplayText(scope.ScopeVersion)})");
        }
        else
        {
            section.AddParagraph("No scope bound.");
        }

        // ── components ──────────────────────────────────────────────────────────────────────────────────────────────
        AddHeading(section, $"Selected components ({r.SelectedComponents.Count})");
        if (r.SelectedComponents.Count == 0)
        {
            section.AddParagraph("None.");
        }
        else
        {
            foreach (var c in r.SelectedComponents)
            {
                section.AddParagraph(
                    $"• {c.KnowledgeContentId:D} — version {DisplayText(c.ContentVersion)}, " +
                    $"language {DisplayText(c.LanguageCode)}, role {DisplayText(c.Role)} " +
                    $"[step {c.Arrangement.TemplateStepId:D}, branch {DisplayText(c.Arrangement.BranchId)}, position {c.Arrangement.Position}]");
            }
        }

        // ── claims ──────────────────────────────────────────────────────────────────────────────────────────────────
        AddHeading(section, $"Selected claims ({r.SelectedClaims.Count})");
        if (r.SelectedClaims.Count == 0)
        {
            section.AddParagraph("None.");
        }
        else
        {
            foreach (var c in r.SelectedClaims)
            {
                section.AddParagraph(
                    $"• {c.ClaimId:D} — version {DisplayText(c.ClaimVersion)} " +
                    $"[step {c.Arrangement.TemplateStepId:D}, branch {DisplayText(c.Arrangement.BranchId)}, position {c.Arrangement.Position}]");
            }
        }

        // ── eligibility ─────────────────────────────────────────────────────────────────────────────────────────────
        AddHeading(section, "Eligibility snapshot");
        if (r.EligibilitySnapshot is { } snap)
        {
            AddField(section, "Evaluated at (UTC)", snap.EvaluatedAtUtc.ToString("u"));
            foreach (var item in snap.Items)
            {
                section.AddParagraph(
                    $"• {DisplayText(item.ItemKind)} {item.ItemId:D} — state {DisplayText(item.State)}" +
                    (string.IsNullOrWhiteSpace(item.BlockingLevel) ? string.Empty : $", blocking {item.BlockingLevel}"));
            }
        }
        else
        {
            section.AddParagraph("No eligibility snapshot.");
        }

        return document;
    }

    private static void AddHeading(Section section, string text)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Size = 13;
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = "10pt";
        p.Format.SpaceAfter = "4pt";
    }

    private static void AddField(Section section, string label, string value)
    {
        var p = section.AddParagraph();
        var l = p.AddFormattedText($"{label}: ", TextFormat.Bold);
        _ = l;
        p.AddText(DisplayText(value));
    }

    private static string DisplayText(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string Sanitize(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "content-set-revision" : name.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        return new string(trimmed.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
