using System.Reflection;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Contracts.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string SourceService = "Diten.Platform";
    private readonly IAuditService _auditService;
    private readonly AuditBehaviorOptions _options;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(
        IAuditService auditService,
        AuditBehaviorOptions options,
        ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _auditService = auditService;
        _options = options;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var auditPlan = BuildAuditPlan(request);
        if (!auditPlan.ShouldAudit)
        {
            return await next();
        }

        TResponse response;

        try
        {
            response = await next();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await AppendRuntimeExceptionAuditAsync(auditPlan.Metadata!, ex, cancellationToken);
            throw;
        }

        var outcome = IsFailureResponse(response) ? AuditOutcome.Failed : AuditOutcome.Succeeded;
        var behaviorMetadata = outcome == AuditOutcome.Failed
            ? BuildLogicalFailureMetadata(response)
            : null;

        var appendResult = await AppendAuditAsync(
            auditPlan.Metadata!,
            outcome,
            behaviorMetadata,
            cancellationToken);

        EnforceAppendResult(appendResult, auditPlan.Metadata!, auditPlan.RequestTypeName);
        return response;
    }

    private AuditPlan BuildAuditPlan(TRequest request)
    {
        var requestType = typeof(TRequest);
        var isAuditableRequest = request is IAuditableRequest;
        var isAuditableCommand = request is IAuditableCommand;
        var isExcluded = request is IAuditExcludedRequest || _options.IsAutoExcluded(requestType);
        var isQuery = IsQueryRequest(requestType);

        if (isExcluded)
        {
            if (isAuditableRequest)
            {
                _logger.LogWarning(
                    "Audit request {RequestType} is both auditable and excluded. Exclusion wins.",
                    requestType.Name);
            }

            return AuditPlan.Skip(requestType.Name);
        }

        if (isQuery)
        {
            if (isAuditableRequest)
            {
                _logger.LogDebug(
                    "Audit request {RequestType} is a query and is not audited by default.",
                    requestType.Name);
            }

            return AuditPlan.Skip(requestType.Name);
        }

        if (!isAuditableCommand)
        {
            return AuditPlan.Skip(requestType.Name);
        }

        if (request is not IAuditMetadataProvider metadataProvider)
        {
            if (_options.RequireMetadataProvider)
            {
                throw new InvalidOperationException(
                    $"Auditable command {requestType.Name} must implement IAuditMetadataProvider. " +
                    "Either implement the provider or set AuditBehaviorOptions.RequireMetadataProvider = false " +
                    "to permit warn-and-skip during a legacy migration window.");
            }

            _logger.LogWarning(
                "Auditable command {RequestType} does not provide audit metadata. Audit append is skipped because RequireMetadataProvider is disabled.",
                requestType.Name);

            return AuditPlan.Skip(requestType.Name);
        }

        return AuditPlan.Audit(requestType.Name, metadataProvider.GetAuditMetadata());
    }

    private async Task<AuditAppendResult> AppendAuditAsync(
        AuditRequestMetadata metadata,
        AuditOutcome outcome,
        IReadOnlyDictionary<string, object?>? behaviorMetadata,
        CancellationToken cancellationToken)
    {
        var appendRequest = new AuditAppendRequest
        {
            CorrelationId = metadata.CorrelationId ?? Guid.NewGuid(),
            RequestType = typeof(TRequest).Name,
            Category = metadata.Category,
            EntityType = metadata.EntityType,
            EntityId = metadata.EntityId,
            Operation = metadata.Operation,
            Outcome = outcome,
            BeforeState = metadata.BeforeState,
            AfterState = metadata.AfterState,
            Metadata = MergeMetadata(metadata.Metadata, behaviorMetadata),
            SourceService = SourceService,
            SourceModule = metadata.SourceModule,
            IsPlatformGlobal = metadata.IsPlatformGlobal,
            Sequence = metadata.Sequence,
            TargetTenantId = metadata.TargetTenantId,
            IsMetaAudit = metadata.IsMetaAudit
        };

        return await _auditService.AppendAsync(appendRequest, cancellationToken);
    }

    private async Task AppendRuntimeExceptionAuditAsync(
        AuditRequestMetadata metadata,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var behaviorMetadata = new Dictionary<string, object?>
        {
            ["AuditBehaviorOutcome"] = "RuntimeException",
            ["ExceptionType"] = exception.GetType().Name,
            ["ExceptionMessage"] = "Unhandled exception during audited request."
        };

        try
        {
            var appendResult = await AppendAuditAsync(metadata, AuditOutcome.Failed, behaviorMetadata, cancellationToken);
            if (appendResult.ShouldBreakBusinessCommand && !_options.IsCritical(metadata.Category))
            {
                _logger.LogWarning(
                    "Critical audit append result for {RequestType} was ignored because category {Category} is not configured as critical.",
                    typeof(TRequest).Name,
                    metadata.Category);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception auditException)
        {
            _logger.LogWarning(
                auditException,
                "Audit append failed while handling runtime exception for {RequestType}. Original exception will be rethrown.",
                typeof(TRequest).Name);
        }
    }

    private void EnforceAppendResult(
        AuditAppendResult appendResult,
        AuditRequestMetadata metadata,
        string requestTypeName)
    {
        if (!appendResult.ShouldBreakBusinessCommand)
        {
            LogControlledAppendResult(appendResult, metadata, requestTypeName);
            return;
        }

        if (!_options.IsCritical(metadata.Category))
        {
            _logger.LogWarning(
                "Audit append for {RequestType} requested business break, but category {Category} is not configured as critical. Command result is preserved.",
                requestTypeName,
                metadata.Category);
            return;
        }

        throw new InvalidOperationException($"Critical audit append failed for {requestTypeName}.");
    }

    private void LogControlledAppendResult(
        AuditAppendResult appendResult,
        AuditRequestMetadata metadata,
        string requestTypeName)
    {
        if (appendResult.Status == AuditAppendStatus.Queued)
        {
            return;
        }

        _logger.LogWarning(
            "Audit append for {RequestType} completed with controlled status {Status} for category {Category}.",
            requestTypeName,
            appendResult.Status,
            metadata.Category);
    }

    private static IReadOnlyDictionary<string, object?> MergeMetadata(
        IReadOnlyDictionary<string, object?>? requestMetadata,
        IReadOnlyDictionary<string, object?>? behaviorMetadata)
    {
        var merged = new Dictionary<string, object?>();

        if (requestMetadata is not null)
        {
            foreach (var pair in requestMetadata)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        if (behaviorMetadata is not null)
        {
            foreach (var pair in behaviorMetadata)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, object?> BuildLogicalFailureMetadata(TResponse response)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["AuditBehaviorOutcome"] = "LogicalFailure"
        };

        if (TryGetPropertyValue(response, "StatusCode") is int statusCode)
        {
            metadata["ResponseStatusCode"] = statusCode;
        }

        if (TryGetPropertyValue(response, "Errors") is IEnumerable<string> errors)
        {
            metadata["ResponseErrors"] = errors.ToArray();
        }

        return metadata;
    }

    private static bool IsFailureResponse(TResponse response)
    {
        return TryGetPropertyValue(response, "IsSuccessful") is false;
    }

    private static object? TryGetPropertyValue(object? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(value);
    }

    private static bool IsQueryRequest(Type requestType)
    {
        return requestType.Name.EndsWith("Query", StringComparison.Ordinal)
               || requestType.Namespace?.Contains(".Queries", StringComparison.Ordinal) == true;
    }

    private sealed record AuditPlan(
        bool ShouldAudit,
        string RequestTypeName,
        AuditRequestMetadata? Metadata)
    {
        public static AuditPlan Skip(string requestTypeName)
        {
            return new AuditPlan(false, requestTypeName, null);
        }

        public static AuditPlan Audit(string requestTypeName, AuditRequestMetadata metadata)
        {
            return new AuditPlan(true, requestTypeName, metadata);
        }
    }
}
