using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record RecordPayrollReconciliationControlCommand(Guid RunId, PayrollReconciliationControlRequest Request) : IRequest<Response<Guid>>;
