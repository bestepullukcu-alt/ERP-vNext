using Diten.BuildingBlocks.BackgroundJobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public sealed class HangfireRecurringJobRegistrationHostedService : IHostedService
{
    private readonly IEnumerable<IRecurringJobRegistrar> _registrars;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly BackgroundJobSchedulerOptions _options;
    private readonly ILogger<HangfireRecurringJobRegistrationHostedService> _logger;

    public HangfireRecurringJobRegistrationHostedService(
        IEnumerable<IRecurringJobRegistrar> registrars,
        IBackgroundJobScheduler scheduler,
        IOptions<BackgroundJobSchedulerOptions> options,
        ILogger<HangfireRecurringJobRegistrationHostedService> logger)
    {
        _registrars = registrars;
        _scheduler = scheduler;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Background job scheduler is disabled; recurring registrations skipped.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registrar in _registrars)
        {
            var registrations = registrar.GetRecurringJobs();
            foreach (var registration in registrations)
            {
                if (!seen.Add(registration.Descriptor.Id))
                {
                    _logger.LogWarning("Duplicate recurring background job id {RecurringJobId} ignored.", registration.Descriptor.Id);
                    continue;
                }

                await _scheduler.RegisterRecurringAsync(registration, cancellationToken);
                _logger.LogInformation(
                    "Recurring background job {RecurringJobId} is {Status}.",
                    registration.Descriptor.Id,
                    registration.Descriptor.IsEnabled ? "registered" : "disabled");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
