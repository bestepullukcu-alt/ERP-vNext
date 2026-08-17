using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class ResolvePayrollIntegrationExceptionValidator : PayrollIntegrationExceptionResolutionRequestValidator<ResolvePayrollIntegrationExceptionCommand>
{
    public ResolvePayrollIntegrationExceptionValidator() : base(x => x.Request) { }
}
