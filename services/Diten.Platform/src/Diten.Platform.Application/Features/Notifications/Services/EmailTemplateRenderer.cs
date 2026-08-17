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
        var fullHtml = string.IsNullOrWhiteSpace(template.BodyHtmlTemplate)
            ? null
            : RenderText(template.BodyHtmlTemplate, variableLookup);
        var fullText = string.IsNullOrWhiteSpace(template.BodyTextTemplate)
            ? null
            : RenderText(template.BodyTextTemplate!, variableLookup);
        var htmlPreview = fullHtml is null ? null : Truncate(fullHtml);
        var textPreview = fullText is null ? null : Truncate(fullText);

        return Response<RenderedEmailTemplateDto>.Success(
            new RenderedEmailTemplateDto(subject, htmlPreview, textPreview, fullHtml, fullText));
    }

    private static string RenderText(string template, IReadOnlyDictionary<string, object?> variables) =>
        TemplateTokenRegex().Replace(template, match =>
        {
            var key = match.Groups["name"].Value.Trim();
            return variables.TryGetValue(key, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        });

    private static string Truncate(string value) =>
        value.Length <= PreviewMaxLength ? value : value[..PreviewMaxLength];

    [GeneratedRegex("\\{\\{\\s*(?<name>[A-Za-z][A-Za-z0-9_.]*)\\s*\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateTokenRegex();
}
