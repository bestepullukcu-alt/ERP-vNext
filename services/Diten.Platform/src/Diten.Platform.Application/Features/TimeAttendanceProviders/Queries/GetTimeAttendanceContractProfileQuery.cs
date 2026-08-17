using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;

public sealed record GetTimeAttendanceContractProfileQuery(Guid ProviderProfileId) : IRequest<Response<TimeAttendanceContractProfileDto>>;
