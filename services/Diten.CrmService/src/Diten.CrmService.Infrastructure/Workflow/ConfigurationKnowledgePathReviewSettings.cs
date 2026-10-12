using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Workflow;

/// <summary>
/// WP-KP-2 — reads <c>Crm:KnowledgePaths:Workflow</c> (TemplateCodeFormat, ReconcileAfterSeconds). A missing value falls
/// back to the documented default; a format without the <c>{0}</c> country slot is refused in favour of the default,
/// since every country would otherwise share one MLR template (the claims rule).
/// </summary>
public sealed class ConfigurationKnowledgePathReviewSettings : IKnowledgePathReviewSettings
{
    public ConfigurationKnowledgePathReviewSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("Crm:KnowledgePaths:Workflow");
        var format = section["TemplateCodeFormat"];
        TemplateCodeFormat = string.IsNullOrWhiteSpace(format) || !format.Contains("{0}", StringComparison.Ordinal)
            ? KnowledgePathReviewDefaults.TemplateCodeFormat
            : format.Trim();

        var seconds = section.GetValue<int?>("ReconcileAfterSeconds");
        ReconcileAfterSeconds = seconds is >= 0 and <= 86400 ? seconds.Value : KnowledgePathReviewDefaults.ReconcileAfterSeconds;
    }

    public string TemplateCodeFormat { get; }
    public int ReconcileAfterSeconds { get; }
}
