using System.Linq.Expressions;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public sealed class HangfireBackgroundJobScheduler : IBackgroundJobScheduler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly BackgroundJobSchedulerOptions _options;

    public HangfireBackgroundJobScheduler(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager,
        IOptions<BackgroundJobSchedulerOptions> options)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
        _options = options.Value;
    }

    public Task<string> EnqueueAsync<TArgs, THandler>(
        TArgs args,
        BackgroundJobContext? context = null,
        CancellationToken cancellationToken = default)
        where THandler : IBackgroundJobHandler<TArgs>
    {
        var descriptor = CreateDescriptor<THandler>(BackgroundJobTriggerTypes.FireAndForget);
        var jobId = _backgroundJobClient.Enqueue(CreateExecutorExpression<THandler, TArgs>(descriptor, args, context, BackgroundJobTriggerTypes.FireAndForget));
        return Task.FromResult(jobId);
    }

    public Task<string> ScheduleAsync<TArgs, THandler>(
        TArgs args,
        DateTimeOffset enqueueAtUtc,
        BackgroundJobContext? context = null,
        CancellationToken cancellationToken = default)
        where THandler : IBackgroundJobHandler<TArgs>
    {
        var descriptor = CreateDescriptor<THandler>(BackgroundJobTriggerTypes.Scheduled);
        var delay = enqueueAtUtc <= DateTimeOffset.UtcNow
            ? TimeSpan.Zero
            : enqueueAtUtc - DateTimeOffset.UtcNow;
        var jobId = _backgroundJobClient.Schedule(CreateExecutorExpression<THandler, TArgs>(descriptor, args, context, BackgroundJobTriggerTypes.Scheduled), delay);
        return Task.FromResult(jobId);
    }

    public Task RegisterRecurringAsync(RecurringJobRegistration registration, CancellationToken cancellationToken = default)
    {
        registration.Validate();
        if (!registration.Descriptor.IsEnabled)
        {
            return Task.CompletedTask;
        }

        var expression = CreateExecutorExpression(
            registration.HandlerType,
            registration.ArgsType,
            registration.Args,
            registration.Context,
            registration.Descriptor,
            BackgroundJobTriggerTypes.Recurring);

        var options = new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            QueueName = registration.Descriptor.Queue ?? "default"
        };

        _recurringJobManager.AddOrUpdate(
            registration.Descriptor.Id,
            expression,
            registration.Descriptor.CronExpression!,
            options);

        return Task.CompletedTask;
    }

    private BackgroundJobDescriptor CreateDescriptor<THandler>(
        string triggerType)
    {
        var jobName = typeof(THandler).Name;
        return new BackgroundJobDescriptor(
            $"{_options.ServiceName}.{jobName}",
            _options.ServiceName,
            jobName,
            "MOD-0026",
            null,
            "UTC",
            true,
            "default",
            _options.DefaultRetryAttempts,
            triggerType);
    }

    private static Expression<Func<HangfireBackgroundJobExecutor, Task>> CreateExecutorExpression<THandler, TArgs>(
        BackgroundJobDescriptor descriptor,
        TArgs args,
        BackgroundJobContext? context,
        string triggerType)
        where THandler : IBackgroundJobHandler<TArgs>
    {
        return CreateExecutorExpression(typeof(THandler), typeof(TArgs), args!, context, descriptor, triggerType);
    }

    private static Expression<Func<HangfireBackgroundJobExecutor, Task>> CreateExecutorExpression(
        Type handlerType,
        Type argsType,
        object args,
        BackgroundJobContext? context,
        BackgroundJobDescriptor descriptor,
        string triggerType)
    {
        var normalizedContext = (context ?? new BackgroundJobContext()) with { TriggerType = triggerType };
        var handlerName = handlerType.AssemblyQualifiedName
                          ?? throw new InvalidOperationException($"Handler {handlerType.FullName} has no assembly-qualified name.");
        var argsName = argsType.AssemblyQualifiedName
                       ?? throw new InvalidOperationException($"Args type {argsType.FullName} has no assembly-qualified name.");
        var argsJson = JsonSerializer.Serialize(args, argsType, JsonOptions);

        return executor => executor.ExecuteAsync(
            handlerName,
            argsName,
            argsJson,
            descriptor,
            normalizedContext,
            null,
            CancellationToken.None);
    }
}
