using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record ResolvePayrollIntegrationExceptionCommand(Guid RunId, Guid ExceptionId, PayrollIntegrationExceptionResolutionRequest Request) : IRequest<Response<NoContent>>;
