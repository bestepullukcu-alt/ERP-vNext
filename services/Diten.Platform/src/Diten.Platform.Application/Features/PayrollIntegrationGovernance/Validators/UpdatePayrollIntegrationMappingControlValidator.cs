using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class UpdatePayrollIntegrationMappingControlValidator : PayrollIntegrationMappingControlRequestValidator<UpdatePayrollIntegrationMappingControlCommand>
{
    public UpdatePayrollIntegrationMappingControlValidator() : base(x => x.Request) { }
}
