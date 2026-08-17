using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class RecordPayrollIntegrationSourceLinkValidator : PayrollIntegrationSourceLinkRequestValidator<RecordPayrollIntegrationSourceLinkCommand>
{
    public RecordPayrollIntegrationSourceLinkValidator() : base(x => x.Request) { }
}
