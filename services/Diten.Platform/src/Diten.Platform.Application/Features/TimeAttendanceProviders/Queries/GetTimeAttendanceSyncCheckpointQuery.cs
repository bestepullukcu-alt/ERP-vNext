using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;

public sealed record GetTimeAttendanceSyncCheckpointQuery(Guid ProviderProfileId) : IRequest<Response<TimeAttendanceSyncCheckpointDto>>;
