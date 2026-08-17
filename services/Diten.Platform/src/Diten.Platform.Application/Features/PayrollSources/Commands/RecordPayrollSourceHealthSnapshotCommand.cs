using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Commands;

public sealed record RecordPayrollSourceHealthSnapshotCommand(Guid SourceProfileId, PayrollSourceHealthSnapshotRequest Request) : IRequest<Response<Guid>>;
