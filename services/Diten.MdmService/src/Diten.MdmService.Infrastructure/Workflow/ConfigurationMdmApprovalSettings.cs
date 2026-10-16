using Diten.MdmService.Application.Features.Approvals;
using Microsoft.Extensions.Configuration;

namespace Diten.MdmService.Infrastructure.Workflow;

/// <summary>WP-VW-W4 (W4-BE-b1) — <c>Mdm:CompetitorProposals:Workflow:TemplateCode</c> (default <c>MDM-COMP-PROPOSAL</c>,
/// used by W4-BE-b2) and <c>Mdm:Workflow:ReconcileAfterSeconds</c> (default 120). Read once at start-up.</summary>
public sealed class ConfigurationMdmApprovalSettings : IMdmApprovalSettings
{
    public ConfigurationMdmApprovalSettings(IConfiguration configuration)
    {
        var template = configuration["Mdm:CompetitorProposals:Workflow:TemplateCode"];
        CompetitorProposalTemplateCode = string.IsNullOrWhiteSpace(template)
            ? MdmApprovalDefaults.CompetitorProposalTemplateCode
            : template.Trim();
        ReconcileAfterSeconds = int.TryParse(configuration["Mdm:Workflow:ReconcileAfterSeconds"], out var seconds) && seconds > 0
            ? seconds
            : MdmApprovalDefaults.ReconcileAfterSeconds;
    }

    public string CompetitorProposalTemplateCode { get; }

    public int ReconcileAfterSeconds { get; }
}
