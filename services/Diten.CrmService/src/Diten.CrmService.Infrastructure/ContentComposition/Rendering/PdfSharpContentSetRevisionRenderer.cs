using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Diten.CrmService.Domain.Entities;
using MigraDoc.DocumentObjectModel;

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
    public RenderedContent Render(ContentSetRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var fileName = $"{Sanitize(revision.RevisionCode)}.pdf";
        return new RenderedContent(MigraDocPdf.ToBytes(BuildDocument(revision)), fileName, "application/pdf");
    }

    private static Document BuildDocument(ContentSetRevision r)
    {
        // WP-KP-3 — the document style / heading / field helpers are shared with the knowledge path renderer.
        var (document, section) = MigraDocPdf.Create(
            string.IsNullOrWhiteSpace(r.RevisionCode) ? "Content Set Revision" : r.RevisionCode,
            "Content Set Revision (rendered artifact)");

        // ── header / identity ───────────────────────────────────────────────────────────────────────────────────────
        MigraDocPdf.AddTitle(section, $"Content Set Revision {DisplayText(r.RevisionCode)}");

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

        // ── context (WP-SB-1R) ──────────────────────────────────────────────────────────────────────────────────────
        // The set context frozen at submit: the set's own country + language, product + audience derived from the
        // template. A pre-SB-1R revision has none (its retired ContentScope binding is not rendered).
        AddHeading(section, "Context");
        if (r.Context is { } context)
        {
            AddField(section, "Country", DisplayText(context.CountryCode));
            AddField(section, "Language", DisplayText(context.LanguageCode));
            AddField(section, "Product", context.ProductId is { } productId
                ? $"{DisplayText(context.ProductCode)} — {DisplayText(context.ProductName)} ({productId:D})"
                : DisplayText(null));
            AddField(section, "Audience", context.AudienceProfileIds.Count == 0
                ? DisplayText(null)
                : string.Join(", ", context.AudienceProfileIds.Select(id => id.ToString("D"))));
        }
        else
        {
            section.AddParagraph("No context recorded (revision submitted before the set context existed).");
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

    private static void AddHeading(Section section, string text) => MigraDocPdf.AddHeading(section, text);

    private static void AddField(Section section, string label, string value) => MigraDocPdf.AddField(section, label, value);

    private static string DisplayText(string? value) => MigraDocPdf.DisplayText(value);

    private static string Sanitize(string? name) => MigraDocPdf.Sanitize(name, "content-set-revision");
}
