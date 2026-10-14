using Diten.CrmService.Application.Features.DoctorInsights;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Workflow;

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2) — reads <c>Crm:DoctorInsights:ConceptTypes:Indication</c> (default <c>CT-IND</c>) and
/// <c>Crm:DoctorInsights:ConceptTypes:Profile</c> (default <c>CT-PROF</c>): the Concept Graph type codes are tenant data,
/// never hard-coded in the reader. A missing value falls back to the default.
/// </summary>
public sealed class ConfigurationDoctorInsightsSettings : IDoctorInsightsSettings
{
    public ConfigurationDoctorInsightsSettings(IConfiguration configuration)
    {
        var indication = configuration["Crm:DoctorInsights:ConceptTypes:Indication"];
        IndicationConceptTypeCode = string.IsNullOrWhiteSpace(indication) ? DoctorInsightsDefaults.IndicationConceptTypeCode : indication.Trim();
        var profile = configuration["Crm:DoctorInsights:ConceptTypes:Profile"];
        ProfileConceptTypeCode = string.IsNullOrWhiteSpace(profile) ? DoctorInsightsDefaults.ProfileConceptTypeCode : profile.Trim();
    }

    public string IndicationConceptTypeCode { get; }
    public string ProfileConceptTypeCode { get; }
}
