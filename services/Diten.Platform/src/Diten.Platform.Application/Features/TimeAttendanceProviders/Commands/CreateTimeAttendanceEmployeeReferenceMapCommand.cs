using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record CreateTimeAttendanceEmployeeReferenceMapCommand(Guid ProviderProfileId, TimeAttendanceEmployeeReferenceMapRequest Request) : IRequest<Response<Guid>>;
