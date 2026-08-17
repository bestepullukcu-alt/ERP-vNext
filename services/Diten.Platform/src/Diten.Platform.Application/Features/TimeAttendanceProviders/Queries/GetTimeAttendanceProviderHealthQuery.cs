using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;

public sealed record GetTimeAttendanceProviderHealthQuery(Guid ProviderProfileId) : IRequest<Response<TimeAttendanceProviderHealthSnapshotDto>>;
