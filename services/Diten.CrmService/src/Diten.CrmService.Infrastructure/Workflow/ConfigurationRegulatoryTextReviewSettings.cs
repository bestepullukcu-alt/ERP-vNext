using Diten.CrmService.Application.Features.Knowledge.Regulatory;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Workflow;

/// <summary>
/// WP-KP-5a — reads <c>Crm:RegulatoryTexts:Workflow</c> (TemplateCodeFormat, ReconcileAfterSeconds). A missing value
/// falls back to the default; a format without the <c>{0}</c> country slot is refused in favour of the default (every
/// country would otherwise share one Regulatory template — the KP-2 / claims rule).
/// </summary>
public sealed class ConfigurationRegulatoryTextReviewSettings : IRegulatoryTextReviewSettings
{
    public ConfigurationRegulatoryTextReviewSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("Crm:RegulatoryTexts:Workflow");
        var format = section["TemplateCodeFormat"];
        TemplateCodeFormat = string.IsNullOrWhiteSpace(format) || !format.Contains("{0}", StringComparison.Ordinal)
            ? RegulatoryTextReviewDefaults.TemplateCodeFormat
            : format.Trim();

        var seconds = section.GetValue<int?>("ReconcileAfterSeconds");
        ReconcileAfterSeconds = seconds is >= 0 and <= 86400 ? seconds.Value : RegulatoryTextReviewDefaults.ReconcileAfterSeconds;
    }

    public string TemplateCodeFormat { get; }
    public int ReconcileAfterSeconds { get; }
}
