using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;

public sealed record GetAttendanceSummaryReferencesQuery(Guid ProviderProfileId) : IRequest<Response<IReadOnlyList<AttendanceSummaryReferenceDto>>>;
