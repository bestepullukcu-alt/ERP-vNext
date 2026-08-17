using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class RecordPayrollIntegrationHealthSnapshotValidator : PayrollIntegrationHealthSnapshotRequestValidator<RecordPayrollIntegrationHealthSnapshotCommand>
{
    public RecordPayrollIntegrationHealthSnapshotValidator() : base(x => x.Request) { }
}
