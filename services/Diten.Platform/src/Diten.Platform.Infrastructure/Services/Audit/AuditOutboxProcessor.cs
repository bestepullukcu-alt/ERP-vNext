using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed class AuditOutboxProcessor
{
    private readonly IAuditOutboxProcessingRepository _outboxRepository;
    private readonly IAuditEventRepository _auditEventRepository;
    private readonly ITenantContext _tenantContext;
    private readonly AuditOutboxPayloadMapper _mapper;
    private readonly AuditOutboxWorkerOptions _options;
    private readonly ILogger<AuditOutboxProcessor> _logger;

    public AuditOutboxProcessor(
        IAuditOutboxProcessingRepository outboxRepository,
        IAuditEventRepository auditEventRepository,
        ITenantContext tenantContext,
        AuditOutboxPayloadMapper mapper,
        AuditOutboxWorkerOptions options,
        ILogger<AuditOutboxProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _auditEventRepository = auditEventRepository;
        _tenantContext = tenantContext;
        _mapper = mapper;
        _options = options;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = await _outboxRepository.ClaimNextBatchAsync(
            _options.BatchSize,
            _options.MaxAttempts,
            now,
            _options.ProcessingStaleAfter,
            ct);

        foreach (var message in messages)
        {
            await ProcessMessageAsync(message, now, ct);
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(AuditOutboxProcessingItem message, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            using var _ = TenantScope.Begin(_tenantContext, message.TenantId);

            if (await IsAlreadyPersistedAsync(message, ct))
            {
                await _outboxRepository.MarkCompletedAsync(message.Id, ct);
                return;
            }

            var auditEvent = _mapper.Map(message, now);
            auditEvent.ValidateAppend();
            await _auditEventRepository.AppendAsync(auditEvent, ct);
            await _outboxRepository.MarkCompletedAsync(message.Id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Best-effort: release the Processing claim so the next iteration / instance
            // can re-claim immediately instead of waiting for ProcessingStaleAfter.
            // If this release itself is cancelled, the stale-Processing recovery path
            // (claim filter Status=Processing AND NextAttemptAtUtc <= now-ProcessingStaleAfter)
            // will pick it up after the threshold.
            await TryReleaseOnCancellationAsync(message, now);
            throw;
        }
        catch (AuditOutboxPayloadMappingException ex)
        {
            await MarkDeadLetterAsync(message, ex, now, ct);
        }
        catch (Exception ex)
        {
            await MarkRetryOrDeadLetterAsync(message, ex, now, ct);
        }
    }

    private async Task TryReleaseOnCancellationAsync(AuditOutboxProcessingItem message, DateTimeOffset now)
    {
        try
        {
            // Use CancellationToken.None so the release write is not itself cancelled
            // during shutdown. This is bounded by the host's shutdown timeout.
            await _outboxRepository.MarkFailedAsync(
                message.Id,
                AuditOutboxStatus.Failed,
                message.Attempts,
                now,
                "Audit outbox processing was cancelled before completion.",
                CancellationToken.None);
        }
        catch (Exception releaseException)
        {
            _logger.LogWarning(
                releaseException,
                "Audit outbox message {MessageId} could not be released on cancellation. Stale-Processing recovery will reclaim it.",
                message.Id);
        }
    }

    private async Task<bool> IsAlreadyPersistedAsync(AuditOutboxProcessingItem message, CancellationToken ct)
    {
        var events = await _auditEventRepository.GetByCorrelationIdAsync(message.CorrelationId, ct);
        return events.Any(auditEvent =>
            auditEvent.Metadata.TryGetValue(AuditOutboxPayloadMapper.OutboxIdempotencyMetadataKey, out var value)
            && string.Equals(value?.ToString(), message.IdempotencyKey, StringComparison.Ordinal));
    }

    private async Task MarkDeadLetterAsync(
        AuditOutboxProcessingItem message,
        Exception exception,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var attempts = Math.Max(message.Attempts + 1, _options.MaxAttempts);
        var lastError = SafeAuditErrorFormatter.Format(exception, _options.MaxLastErrorLength);
        await _outboxRepository.MarkFailedAsync(
            message.Id,
            AuditOutboxStatus.DeadLetter,
            attempts,
            now,
            lastError,
            ct);

        _logger.LogWarning(
            "Audit outbox message {MessageId} moved to DeadLetter. ErrorType={ErrorType}",
            message.Id,
            exception.GetType().Name);
    }

    private async Task MarkRetryOrDeadLetterAsync(
        AuditOutboxProcessingItem message,
        Exception exception,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var attempts = message.Attempts + 1;
        var status = attempts >= _options.MaxAttempts ? AuditOutboxStatus.DeadLetter : AuditOutboxStatus.Failed;
        var nextAttemptAtUtc = status == AuditOutboxStatus.DeadLetter
            ? now
            : now.Add(_options.GetRetryDelay(attempts));
        var lastError = SafeAuditErrorFormatter.Format(exception, _options.MaxLastErrorLength);

        await _outboxRepository.MarkFailedAsync(
            message.Id,
            status,
            attempts,
            nextAttemptAtUtc,
            lastError,
            ct);

        _logger.LogWarning(
            "Audit outbox message {MessageId} processing failed with status {Status}. Attempts={Attempts}. ErrorType={ErrorType}",
            message.Id,
            status,
            attempts,
            exception.GetType().Name);
    }
}
