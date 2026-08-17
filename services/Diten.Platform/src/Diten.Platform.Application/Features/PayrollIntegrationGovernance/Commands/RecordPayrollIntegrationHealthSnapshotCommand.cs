using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record RecordPayrollIntegrationHealthSnapshotCommand(PayrollIntegrationHealthSnapshotRequest Request) : IRequest<Response<Guid>>;
