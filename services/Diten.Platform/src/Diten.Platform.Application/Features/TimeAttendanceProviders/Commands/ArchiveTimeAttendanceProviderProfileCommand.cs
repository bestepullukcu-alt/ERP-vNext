using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record ArchiveTimeAttendanceProviderProfileCommand(Guid Id) : IRequest<Response<NoContent>>;
