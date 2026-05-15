using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public sealed class HangfireBackgroundJobExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobExecutionLogWriter _logWriter;
    private readonly ILogger<HangfireBackgroundJobExecutor> _logger;

    public HangfireBackgroundJobExecutor(
        IServiceProvider serviceProvider,
        IJobExecutionLogWriter logWriter,
        ILogger<HangfireBackgroundJobExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _logWriter = logWriter;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        string handlerTypeName,
        string argsTypeName,
        string argsJson,
        BackgroundJobDescriptor descriptor,
        BackgroundJobContext context,
        PerformContext? performContext = null,
        CancellationToken cancellationToken = default)
    {
        descriptor.Validate();

        var retryCount = GetRetryCount(performContext);
        var executionContext = context with
        {
            CorrelationId = context.CorrelationId ?? Guid.NewGuid(),
            RetryCount = retryCount,
            TriggerType = string.IsNullOrWhiteSpace(context.TriggerType)
                ? descriptor.TriggerType
                : context.TriggerType
        };

        var startedLog = await _logWriter.StartedAsync(
            descriptor,
            executionContext,
            performContext?.BackgroundJob?.Id,
            cancellationToken);

        try
        {
            await InvokeHandlerAsync(handlerTypeName, argsTypeName, argsJson, executionContext, cancellationToken);
            await _logWriter.SucceededAsync(startedLog, DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background job {JobName} failed with correlation {CorrelationId}.", descriptor.JobName, startedLog.CorrelationId);
            await _logWriter.FailedAsync(startedLog, ex, retryCount, DateTimeOffset.UtcNow, cancellationToken);
            throw;
        }
    }

    private async Task InvokeHandlerAsync(
        string handlerTypeName,
        string argsTypeName,
        string argsJson,
        BackgroundJobContext context,
        CancellationToken cancellationToken)
    {
        var handlerType = Type.GetType(handlerTypeName, throwOnError: true)!;
        var argsType = Type.GetType(argsTypeName, throwOnError: true)!;
        var args = JsonSerializer.Deserialize(argsJson, argsType, JsonOptions)
                   ?? throw new InvalidOperationException($"Unable to deserialize background job arguments for {argsType.FullName}.");

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService(handlerType);
        var contractType = typeof(IBackgroundJobHandler<>).MakeGenericType(argsType);
        var handleMethod = contractType.GetMethod(nameof(IBackgroundJobHandler<object>.HandleAsync))
                           ?? throw new MissingMethodException(contractType.FullName, nameof(IBackgroundJobHandler<object>.HandleAsync));

        object? result;
        try
        {
            result = handleMethod.Invoke(handler, [args, context, cancellationToken]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException($"Background job handler {handlerType.FullName} returned a non-task result.");
    }

    private static int GetRetryCount(PerformContext? performContext)
    {
        if (performContext is null)
        {
            return 0;
        }

        try
        {
            return Math.Max(0, performContext.GetJobParameter<int>("RetryCount"));
        }
        catch
        {
            return 0;
        }
    }
}
