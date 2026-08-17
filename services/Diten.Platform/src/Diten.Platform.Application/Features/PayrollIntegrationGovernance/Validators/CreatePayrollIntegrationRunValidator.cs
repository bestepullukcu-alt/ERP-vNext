using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class CreatePayrollIntegrationRunValidator : PayrollIntegrationRunRequestValidator<CreatePayrollIntegrationRunCommand>
{
    public CreatePayrollIntegrationRunValidator() : base(x => x.Request) { }
}
