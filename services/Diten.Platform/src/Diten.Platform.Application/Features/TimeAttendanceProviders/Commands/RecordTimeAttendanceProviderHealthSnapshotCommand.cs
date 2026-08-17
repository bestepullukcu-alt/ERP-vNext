using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record RecordTimeAttendanceProviderHealthSnapshotCommand(Guid ProviderProfileId, TimeAttendanceProviderHealthSnapshotRequest Request) : IRequest<Response<Guid>>;
