using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.ContentComposition;

/// <summary>
/// WP-CL-BE-4 — reads <c>Crm:Claims:Workflow</c> (CoreTemplateCode, LocalTemplateCodeFormat, ReconcileAfterSeconds).
/// A missing value falls back to the documented default; a local format without the <c>{0}</c> country slot is
/// refused in favour of the default, since every country would otherwise share one template.
/// </summary>
public sealed class ConfigurationClaimWorkflowSettings : IClaimWorkflowSettings
{
    public ConfigurationClaimWorkflowSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("Crm:Claims:Workflow");
        var core = section["CoreTemplateCode"];
        CoreTemplateCode = string.IsNullOrWhiteSpace(core) ? ClaimWorkflowDefaults.CoreTemplateCode : core.Trim();

        var local = section["LocalTemplateCodeFormat"];
        LocalTemplateCodeFormat = string.IsNullOrWhiteSpace(local) || !local.Contains("{0}", StringComparison.Ordinal)
            ? ClaimWorkflowDefaults.LocalTemplateCodeFormat
            : local.Trim();

        var seconds = section.GetValue<int?>("ReconcileAfterSeconds");
        ReconcileAfterSeconds = seconds is >= 0 and <= 86400 ? seconds.Value : ClaimWorkflowDefaults.ReconcileAfterSeconds;
    }

    public string CoreTemplateCode { get; }
    public string LocalTemplateCodeFormat { get; }
    public int ReconcileAfterSeconds { get; }
}
