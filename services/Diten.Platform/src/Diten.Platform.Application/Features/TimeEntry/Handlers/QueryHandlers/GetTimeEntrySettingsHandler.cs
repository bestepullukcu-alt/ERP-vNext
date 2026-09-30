using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>The tenant's settings; a tenant that never saved any reads "no pool, reminder off, version 0".</summary>
public sealed class GetTimeEntrySettingsHandler : IRequestHandler<GetTimeEntrySettingsQuery, Response<TimeEntrySettingsDto>>
{
    private readonly ITimeEntrySettingsRepository _settings;

    public GetTimeEntrySettingsHandler(ITimeEntrySettingsRepository settings) => _settings = settings;

    public async Task<Response<TimeEntrySettingsDto>> Handle(GetTimeEntrySettingsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await _settings.GetAsync(ct);
        return Response<TimeEntrySettingsDto>.Success(
            new TimeEntrySettingsDto(settings?.TimeAdminPoolPositionId, settings?.Version ?? 0, settings?.WeeklyReminderEnabled ?? false),
            correlationId: request.CorrelationId);
    }
}
