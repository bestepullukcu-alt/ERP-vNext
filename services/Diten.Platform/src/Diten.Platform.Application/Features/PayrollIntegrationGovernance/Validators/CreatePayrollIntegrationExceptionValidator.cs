using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class CreatePayrollIntegrationExceptionValidator : PayrollIntegrationExceptionRequestValidator<CreatePayrollIntegrationExceptionCommand>
{
    public CreatePayrollIntegrationExceptionValidator() : base(x => x.Request) { }
}
