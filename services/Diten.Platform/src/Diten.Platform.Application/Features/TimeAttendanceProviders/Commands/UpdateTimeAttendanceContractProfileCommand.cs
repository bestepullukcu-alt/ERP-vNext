using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record UpdateTimeAttendanceContractProfileCommand(Guid ProviderProfileId, TimeAttendanceContractProfileRequest Request) : IRequest<Response<NoContent>>;
