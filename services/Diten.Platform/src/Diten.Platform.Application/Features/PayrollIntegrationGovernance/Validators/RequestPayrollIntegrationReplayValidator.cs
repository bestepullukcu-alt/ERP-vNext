using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

public sealed class RequestPayrollIntegrationReplayValidator : PayrollIntegrationRetryReplayRequestValidator<RequestPayrollIntegrationReplayCommand>
{
    public RequestPayrollIntegrationReplayValidator() : base(x => x.Request) { }
}
