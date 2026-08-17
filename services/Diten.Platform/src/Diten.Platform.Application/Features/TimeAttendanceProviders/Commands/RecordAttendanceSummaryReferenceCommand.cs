using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

public sealed record RecordAttendanceSummaryReferenceCommand(Guid ProviderProfileId, AttendanceSummaryReferenceRequest Request) : IRequest<Response<Guid>>;
