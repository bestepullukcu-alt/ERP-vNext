using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.WorkingCalendarImport;

public sealed record HolidayAutoFetchJobArgs(IReadOnlyList<int> YearOffsets, int MaxBatchesPerRun, bool IncludeNonPublicTypes);

public sealed class HolidayAutoFetchJob : IBackgroundJobHandler<HolidayAutoFetchJobArgs>
{
    private readonly IMediator _mediator;
    private readonly IWorkingCalendarRepository _calendars;
    private readonly WorkingCalendarImportOptions _options;
    private readonly BackgroundJobSchedulerOptions _schedulerOptions;
    private readonly IJobExecutionLogWriter _logs;

    public HolidayAutoFetchJob(IMediator mediator, IWorkingCalendarRepository calendars,
        IOptions<WorkingCalendarImportOptions> options, IOptions<BackgroundJobSchedulerOptions> schedulerOptions,
        IJobExecutionLogWriter logs)
        => (_mediator, _calendars, _options, _schedulerOptions, _logs) =
            (mediator, calendars, options.Value, schedulerOptions.Value, logs);

    public async Task HandleAsync(HolidayAutoFetchJobArgs args, BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        var descriptor = new BackgroundJobDescriptor("Diten.Platform.WorkingCalendar.HolidayAutoFetchJob",
            "Diten.Platform", nameof(HolidayAutoFetchJob), "working-calendar", _options.Schedule.CronExpression,
            "UTC", true, "platform", _schedulerOptions.DefaultRetryAttempts, BackgroundJobTriggerTypes.Recurring);
        var started = await _logs.StartedAsync(descriptor, context, cancellationToken: cancellationToken);
        try
        {
            if (_options.Enabled)
            {
                var years = args.YearOffsets.Select(x => DateTime.UtcNow.Year + x).ToHashSet();
                var targets = (await _calendars.ListCountryLayerAsync(cancellationToken))
                    .Where(x => x.IsActive() && years.Contains(x.CalendarYear)).Take(args.MaxBatchesPerRun);
                foreach (var target in targets)
                {
                    try
                    {
                        await _mediator.Send(new StartWorkingCalendarImportCommand(target.Id,
                            args.IncludeNonPublicTypes, null, "scheduled", WorkingCalendarImportActors.Scheduler,
                            $"{target.CountryCode}:{target.CalendarYear}:{target.Id:N}"), cancellationToken);
                    }
                    catch
                    {
                        // Target-scoped fail-closed: a failed fetch does not stop or mutate another target.
                    }
                }
            }
            await _logs.SucceededAsync(started, DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            await _logs.FailedAsync(started, ex, 0, DateTimeOffset.UtcNow, cancellationToken);
            throw;
        }
    }
}
