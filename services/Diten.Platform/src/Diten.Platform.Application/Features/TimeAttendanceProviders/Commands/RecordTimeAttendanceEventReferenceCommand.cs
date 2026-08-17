using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record RecordTimeAttendanceEventReferenceCommand(Guid ProviderProfileId, TimeAttendanceEventReferenceRequest Request) : IRequest<Response<Guid>>;
