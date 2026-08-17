using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class RecordPayrollReconciliationControlValidator : PayrollReconciliationControlRequestValidator<RecordPayrollReconciliationControlCommand>
{
    public RecordPayrollReconciliationControlValidator() : base(x => x.Request) { }
}
