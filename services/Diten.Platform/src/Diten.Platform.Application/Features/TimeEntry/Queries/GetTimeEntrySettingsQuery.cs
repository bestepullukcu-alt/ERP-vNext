using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

public sealed record GetTimeEntrySettingsQuery(string CorrelationId) : IRequest<Response<TimeEntrySettingsDto>>;
