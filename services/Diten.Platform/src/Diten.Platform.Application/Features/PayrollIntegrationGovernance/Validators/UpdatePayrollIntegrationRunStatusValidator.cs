using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class UpdatePayrollIntegrationRunStatusValidator : PayrollIntegrationRunStatusRequestValidator<UpdatePayrollIntegrationRunStatusCommand>
{
    public UpdatePayrollIntegrationRunStatusValidator() : base(x => x.Request) { }
}
