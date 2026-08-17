using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.BackgroundJobs;

public sealed class PlatformRecurringJobRegistrar : IRecurringJobRegistrar
{
    private const string ServiceName = "Diten.Platform";
    private readonly BackgroundJobSchedulerOptions _options;

    public PlatformRecurringJobRegistrar(IOptions<BackgroundJobSchedulerOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyCollection<RecurringJobRegistration> GetRecurringJobs()
    {
        var registrations = new List<RecurringJobRegistration>
        {
            CreateDeferred("Diten.Platform.MOD-0297.TrialExpiryScanJob", "TrialExpiryScanJob", "MOD-0297", "0 2 * * *"),
            CreateDeferred("Diten.Platform.MOD-0297.SubscriptionRenewalJob", "SubscriptionRenewalJob", "MOD-0297/MOD-0299", "0 3 * * *"),
            CreateDeferred("Diten.Platform.MOD-0033.QuotaResetJob", "QuotaResetJob", "MOD-0033", "0 0 1 * *"),
            CreateDeferred("Diten.Platform.MOD-0018.EntitlementCacheRefreshJob", "EntitlementCacheRefreshJob", "MOD-0018", "0 * * * *"),
            CreateDeferred("Diten.Platform.MOD-0034.WebhookRetryJob", "WebhookRetryJob", "MOD-0034", "*/5 * * * *"),
            CreateDeferred("Diten.Platform.MOD-0021.AuditLogArchiveJob", "AuditLogArchiveJob", "MOD-0021", "0 4 * * 0"),
            CreateEmailDispatchSweepRegistration(),
            CreateDeferred("Diten.Platform.MOD-0009.ProvisioningRetryJob", "ProvisioningRetryJob", "MOD-0009", "*/2 * * * *")
        };

        return registrations;
    }

    private RecurringJobRegistration CreateEmailDispatchSweepRegistration()
    {
        const string id = "Diten.Platform.MOD-0027.EmailDispatchJob";
        const string jobName = "EmailDispatchSweepJob";
        const string owner = "MOD-0027";
        const string cron = "* * * * *";

        var enabled = _options.RegisterStandardJobs
                      && _options.EnabledJobs.TryGetValue(id, out var configuredEnabled)
                      && configuredEnabled;

        var descriptor = new BackgroundJobDescriptor(
            Id: id,
            ServiceName: ServiceName,
            JobName: jobName,
            Owner: owner,
            CronExpression: cron,
            TimeZoneId: "UTC",
            IsEnabled: enabled,
            Queue: "platform",
            MaxRetryAttempts: _options.DefaultRetryAttempts,
            TriggerType: BackgroundJobTriggerTypes.Recurring);

        return new RecurringJobRegistration(
            descriptor,
            typeof(EmailDispatchSweepJob),
            typeof(EmailDispatchSweepJobArgs),
            new EmailDispatchSweepJobArgs(BatchSize: 50, MaxRetryCount: Math.Max(1, _options.DefaultRetryAttempts)),
            new BackgroundJobContext(
                TriggerType: BackgroundJobTriggerTypes.Recurring,
                TriggeredBy: nameof(PlatformRecurringJobRegistrar),
                Metadata: new Dictionary<string, string>
                {
                    ["owner"] = owner,
                    ["execution"] = "sweep"
                }));
    }

    private RecurringJobRegistration CreateDeferred(string id, string jobName, string owner, string cron, string? reason = null)
    {
        var enabled = _options.RegisterStandardJobs
                      && _options.EnabledJobs.TryGetValue(id, out var configuredEnabled)
                      && configuredEnabled;

        var descriptor = new BackgroundJobDescriptor(
            Id: id,
            ServiceName: ServiceName,
            JobName: jobName,
            Owner: owner,
            CronExpression: cron,
            TimeZoneId: "UTC",
            IsEnabled: enabled,
            Queue: "platform",
            MaxRetryAttempts: _options.DefaultRetryAttempts,
            TriggerType: BackgroundJobTriggerTypes.Recurring);

        return new RecurringJobRegistration(
            descriptor,
            typeof(DeferredPlatformJobHandler),
            typeof(DeferredPlatformJobArgs),
            new DeferredPlatformJobArgs(owner, reason ?? "Real execution belongs to the owning module pack."),
            new BackgroundJobContext(
                TriggerType: BackgroundJobTriggerTypes.Recurring,
                TriggeredBy: "PlatformRecurringJobRegistrar",
                Metadata: new Dictionary<string, string>
                {
                    ["owner"] = owner,
                    ["execution"] = "deferred"
                }));
    }
}
