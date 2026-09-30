using System.Net;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications.Services;

public sealed partial class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private const int PreviewMaxLength = 2000;

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

        var subject = RenderText(template.SubjectTemplate, variableLookup);
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
        TemplateTokenRegex().Replace(template, match =>
        {
            var key = match.Groups["name"].Value.Trim();
            var text = variables.TryGetValue(key, out var value)
                ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            return encode is null ? text : encode(text);
        });

    private static string Truncate(string value) =>
        value.Length <= PreviewMaxLength ? value : value[..PreviewMaxLength];

    [GeneratedRegex("\\{\\{\\s*(?<name>[A-Za-z][A-Za-z0-9_.]*)\\s*\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateTokenRegex();
}
