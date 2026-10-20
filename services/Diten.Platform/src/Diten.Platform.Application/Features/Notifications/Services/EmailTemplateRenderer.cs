using System.Net;
using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications.Services;

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public const int PreviewMaxLength = 2000;

    public Response<RenderedEmailTemplateDto> Render(
        NotificationTemplate template,
        IReadOnlyDictionary<string, object?> variables)
    {
        var variableLookup = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase);
        var missing = template.Variables
            .Where(x => x.IsRequired && !variableLookup.ContainsKey(x.Name))
            .Select(x => x.Name)
            .ToArray();

        if (missing.Length > 0)
        {
            return Response<RenderedEmailTemplateDto>.Fail(
                $"Missing required template variable(s): {string.Join(", ", missing)}.",
                400);
        }

        var subject = WithoutEmptySegments(RenderText(template.SubjectTemplate, variableLookup));
        // A variable is DATA, never markup: in the HTML body every value is HTML-encoded (2026-09-30, MOD-0280-FU01 T3
        // review — an approver's free-text reject reason could otherwise put a live foreign link into a system-branded
        // e-mail). No template carries markup through a variable; subject and text body are plain text and stay as-is.
        var fullHtml = string.IsNullOrWhiteSpace(template.BodyHtmlTemplate)
            ? null
            : RenderText(template.BodyHtmlTemplate, variableLookup, WebUtility.HtmlEncode);
        var fullText = string.IsNullOrWhiteSpace(template.BodyTextTemplate)
            ? null
            : RenderText(template.BodyTextTemplate!, variableLookup);
        var htmlPreview = fullHtml is null ? null : Truncate(fullHtml);
        var textPreview = fullText is null ? null : Truncate(fullText);

        return Response<RenderedEmailTemplateDto>.Success(
            new RenderedEmailTemplateDto(subject, htmlPreview, textPreview, fullHtml, fullText));
    }

    private static string RenderText(
        string template, IReadOnlyDictionary<string, object?> variables, Func<string, string>? encode = null) =>
        TemplateTokens.Render(template, variables, encode);

    /// <summary>The separator of a subject made of parts ("Visit report · {{doctorName}} · {{visitDate}}").</summary>
    public const string SubjectSegmentSeparator = " · ";

    /// <summary>
    /// WP-VW-W5 W5-FIX2 (F-4) — a subject part whose variables were all empty is left out together with its separator,
    /// instead of leaving "· ·" (a pharmacy visit has no doctor; a report may name no product). Only a subject that uses the
    /// separator is touched, and only a part that is blank once rendered; every other subject is returned as it was.
    /// </summary>
    public static string WithoutEmptySegments(string subject)
    {
        // Split on the WHOLE separator (spaces included): a name with a bare middle dot ("约翰·史密斯") is one part.
        if (string.IsNullOrEmpty(subject) || !subject.Contains(SubjectSegmentSeparator, StringComparison.Ordinal))
        {
            return subject;
        }

        var parts = subject.Split(SubjectSegmentSeparator, StringSplitOptions.None);
        if (parts.All(part => !string.IsNullOrWhiteSpace(part)))
        {
            return subject;
        }

        return string.Join(SubjectSegmentSeparator, parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }

    private static string Truncate(string value) =>
        value.Length <= PreviewMaxLength ? value : value[..PreviewMaxLength];
}
