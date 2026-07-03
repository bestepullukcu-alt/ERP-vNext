using System.Globalization;
using System.Text;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — deterministic key generation.
/// <c>DocumentKey = {tenantId}|{companyId}|{collectionInstanceId}|{slug(title)}</c>;
/// <c>TemplateKey = {tenantId}|{companyId}|{collectionInstanceId?}|{slug(title)}</c>.
/// </summary>
public sealed class DocumentKeyFactory
{
    public string ForDocument(Guid tenantId, Guid companyId, Guid collectionInstanceId, string title) =>
        string.Join('|', tenantId.ToString("D"), companyId.ToString("D"), collectionInstanceId.ToString("D"), Slug(title));

    public string ForTemplate(Guid tenantId, Guid companyId, Guid? collectionInstanceId, string title) =>
        string.Join('|',
            tenantId.ToString("D"),
            companyId.ToString("D"),
            collectionInstanceId?.ToString("D") ?? "none",
            Slug(title));

    public static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "untitled";
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastDash = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastDash = false;
            }
            else if (!lastDash && builder.Length > 0)
            {
                builder.Append('-');
                lastDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "untitled" : slug;
    }
}
