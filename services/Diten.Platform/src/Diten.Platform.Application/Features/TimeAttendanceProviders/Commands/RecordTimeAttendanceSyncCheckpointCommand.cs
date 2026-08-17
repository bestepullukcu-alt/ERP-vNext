using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record RecordTimeAttendanceSyncCheckpointCommand(Guid ProviderProfileId, TimeAttendanceSyncCheckpointRequest Request) : IRequest<Response<Guid>>;
