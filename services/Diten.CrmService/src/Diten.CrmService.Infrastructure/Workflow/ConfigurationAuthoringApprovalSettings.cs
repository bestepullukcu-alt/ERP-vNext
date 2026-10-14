using Diten.CrmService.Application.Features.Approvals;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Workflow;

/// <summary>
/// WP-VW-W3 (W3-BE-c) — reads <c>Crm:ObjectionResponses:Workflow:TemplateCode</c> (default <c>CRM-OBJ-RESP</c>),
/// <c>Crm:VisitEvidenceExceptions:Workflow:TemplateCode</c> (default <c>CRM-VISIT-EVIDENCE-EXC</c>) and the reconcile
/// window (<c>Crm:ObjectionResponses:Workflow:ReconcileAfterSeconds</c>, default 120). A missing value falls back to the
/// default; the templates themselves are created by W3-CFG.
/// </summary>
public sealed class ConfigurationAuthoringApprovalSettings : IAuthoringApprovalSettings
{
    public ConfigurationAuthoringApprovalSettings(IConfiguration configuration)
    {
        var objection = configuration["Crm:ObjectionResponses:Workflow:TemplateCode"];
        ObjectionResponseTemplateCode = string.IsNullOrWhiteSpace(objection)
            ? AuthoringApprovalDefaults.ObjectionResponseTemplateCode
            : objection.Trim();

        var exception = configuration["Crm:VisitEvidenceExceptions:Workflow:TemplateCode"];
        VisitEvidenceExceptionTemplateCode = string.IsNullOrWhiteSpace(exception)
            ? AuthoringApprovalDefaults.VisitEvidenceExceptionTemplateCode
            : exception.Trim();

        var seconds = configuration.GetSection("Crm:ObjectionResponses:Workflow").GetValue<int?>("ReconcileAfterSeconds");
        ReconcileAfterSeconds = seconds is >= 0 and <= 86400 ? seconds.Value : AuthoringApprovalDefaults.ReconcileAfterSeconds;
    }

    public string ObjectionResponseTemplateCode { get; }
    public string VisitEvidenceExceptionTemplateCode { get; }
    public int ReconcileAfterSeconds { get; }
}
