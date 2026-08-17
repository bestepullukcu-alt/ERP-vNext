using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record UpdateTimeAttendanceEmployeeReferenceMapCommand(Guid ProviderProfileId, Guid MapId, TimeAttendanceEmployeeReferenceMapRequest Request) : IRequest<Response<NoContent>>;
