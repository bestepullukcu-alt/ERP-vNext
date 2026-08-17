using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class RecordPayrollIntegrationEvidenceExportReferenceValidator : PayrollIntegrationEvidenceExportReferenceRequestValidator<RecordPayrollIntegrationEvidenceExportReferenceCommand>
{
    public RecordPayrollIntegrationEvidenceExportReferenceValidator() : base(x => x.Request) { }
}
