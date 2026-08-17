using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record RecordPayrollIntegrationSourceLinkCommand(Guid RunId, PayrollIntegrationSourceLinkRequest Request) : IRequest<Response<Guid>>;
