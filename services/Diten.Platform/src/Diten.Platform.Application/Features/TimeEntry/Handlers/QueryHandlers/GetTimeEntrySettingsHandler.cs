using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>The tenant's settings; a tenant that never saved any reads "no pool, reminder off, version 0".</summary>
public sealed class GetTimeEntrySettingsHandler : IRequestHandler<GetTimeEntrySettingsQuery, Response<TimeEntrySettingsDto>>
{
    private readonly ITimeEntrySettingsRepository _settings;
    private readonly BackgroundJobSchedulerOptions _jobs;

    public GetTimeEntrySettingsHandler(ITimeEntrySettingsRepository settings, IOptions<BackgroundJobSchedulerOptions> jobs)
    {
        _settings = settings;
        _jobs = jobs.Value;
    }

    public async Task<Response<TimeEntrySettingsDto>> Handle(GetTimeEntrySettingsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await _settings.GetAsync(ct);
        return Response<TimeEntrySettingsDto>.Success(
            new TimeEntrySettingsDto(settings?.TimeAdminPoolPositionId, settings?.Version ?? 0, settings?.WeeklyReminderEnabled ?? false,
                TimesheetReminderJob.IsScheduled(_jobs)),
            correlationId: request.CorrelationId);
    }
}
