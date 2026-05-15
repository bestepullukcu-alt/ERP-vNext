using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditOutboxWorkerTests
{
    [Fact]
    public async Task Processor_ShouldMapPendingOutboxMessageToAuditEvent()
    {
        var item = CreateItem();
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var auditEvents = new FakeAuditEventRepository();
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        var auditEvent = Assert.Single(auditEvents.Appended);
        Assert.Equal(item.TenantId, auditEvent.TenantId);
        Assert.Equal(item.CorrelationId, auditEvent.CorrelationId);
        Assert.Equal(AuditActorType.PlatformAdministrator, auditEvent.ActorType);
        Assert.Equal(AuditCategory.Security, auditEvent.Category);
        Assert.Equal("TestEntity", auditEvent.EntityType);
        Assert.Equal(AuditOperation.Update, auditEvent.Operation);
        Assert.Equal(AuditOutcome.Succeeded, auditEvent.Outcome);
        Assert.Equal(item.IdempotencyKey, auditEvent.Metadata[AuditOutboxPayloadMapper.OutboxIdempotencyMetadataKey]);
    }

    [Fact]
    public async Task Processor_ShouldSetWrittenAtUtcAtPersistCallSite()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var outbox = new FakeAuditOutboxProcessingRepository(CreateItem());
        var auditEvents = new FakeAuditEventRepository();
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        var auditEvent = Assert.Single(auditEvents.Appended);
        Assert.InRange(auditEvent.WrittenAtUtc, before, after);
    }

    [Fact]
    public async Task Processor_ShouldMarkCompletedAfterSuccessfulAppend()
    {
        var item = CreateItem();
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var processor = CreateProcessor(outbox, new FakeAuditEventRepository());

        await processor.ProcessBatchAsync();

        Assert.Contains(item.Id, outbox.CompletedIds);
        Assert.Empty(outbox.Failures);
    }

    [Fact]
    public async Task Processor_ShouldMarkFailedAndScheduleRetryWithSafeLastError()
    {
        var item = CreateItem();
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var auditEvents = new FakeAuditEventRepository
        {
            AppendException = new InvalidOperationException("password raw-secret leaked here")
        };
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        var failure = Assert.Single(outbox.Failures);
        Assert.Equal(AuditOutboxStatus.Failed, failure.Status);
        Assert.Equal(1, failure.Attempts);
        Assert.True(failure.NextAttemptAtUtc > item.NextAttemptAtUtc);
        Assert.Contains(nameof(InvalidOperationException), failure.LastError);
        Assert.DoesNotContain("raw-secret", failure.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", failure.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Processor_ShouldMoveToDeadLetterAfterMaxAttempts()
    {
        var item = CreateItem(attempts: 4);
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var auditEvents = new FakeAuditEventRepository
        {
            AppendException = new InvalidOperationException("transient failure")
        };
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        var failure = Assert.Single(outbox.Failures);
        Assert.Equal(AuditOutboxStatus.DeadLetter, failure.Status);
        Assert.Equal(5, failure.Attempts);
    }

    [Fact]
    public async Task Processor_ShouldDeadLetterInvalidPayload()
    {
        var item = CreateItem(payloadOverrides: new Dictionary<string, object?> { ["Category"] = "NotACategory" });
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var auditEvents = new FakeAuditEventRepository();
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        Assert.Empty(auditEvents.Appended);
        var failure = Assert.Single(outbox.Failures);
        Assert.Equal(AuditOutboxStatus.DeadLetter, failure.Status);
        Assert.Contains("Field=Category", failure.LastError);
        Assert.DoesNotContain("NotACategory", failure.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Processor_ShouldTreatExistingIdempotencyKeyAsCompletedWithoutSecondAppend()
    {
        var item = CreateItem();
        var existingEvent = new AuditEvent
        {
            TenantId = item.TenantId,
            CorrelationId = item.CorrelationId,
            Category = AuditCategory.Security,
            EntityType = "TestEntity",
            Operation = AuditOperation.Update,
            Outcome = AuditOutcome.Succeeded,
            SourceService = "Diten.Platform",
            Metadata = new Dictionary<string, object?>
            {
                [AuditOutboxPayloadMapper.OutboxIdempotencyMetadataKey] = item.IdempotencyKey
            }
        };
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var auditEvents = new FakeAuditEventRepository(existingEvent);
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        Assert.Empty(auditEvents.Appended);
        Assert.Contains(item.Id, outbox.CompletedIds);
    }

    [Fact]
    public async Task Processor_ShouldHandleEnumParseErrorsAsControlledDeadLetter()
    {
        var item = CreateItem(payloadOverrides: new Dictionary<string, object?> { ["Operation"] = "Explode" });
        var outbox = new FakeAuditOutboxProcessingRepository(item);
        var processor = CreateProcessor(outbox, new FakeAuditEventRepository());

        await processor.ProcessBatchAsync();

        var failure = Assert.Single(outbox.Failures);
        Assert.Equal(AuditOutboxStatus.DeadLetter, failure.Status);
        Assert.Contains("Field=Operation", failure.LastError);
        Assert.DoesNotContain("Explode", failure.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Processor_ShouldPreserveAuditEventImmutability()
    {
        var outbox = new FakeAuditOutboxProcessingRepository(CreateItem());
        var auditEvents = new FakeAuditEventRepository();
        var processor = CreateProcessor(outbox, auditEvents);

        await processor.ProcessBatchAsync();

        var auditEvent = Assert.Single(auditEvents.Appended);
        Assert.False(auditEvent.IsDeleted);
        Assert.Null(auditEvent.UpdatedAt);
        Assert.Null(auditEvent.UpdatedBy);
        auditEvent.ValidateAppend();
    }

    private static AuditOutboxProcessor CreateProcessor(
        FakeAuditOutboxProcessingRepository outbox,
        FakeAuditEventRepository auditEvents)
    {
        return new AuditOutboxProcessor(
            outbox,
            auditEvents,
            new TenantContext(),
            new AuditOutboxPayloadMapper(),
            new AuditOutboxWorkerOptions
            {
                BatchSize = 10,
                MaxAttempts = 5,
                InitialRetryDelay = TimeSpan.FromSeconds(30),
                MaxRetryDelay = TimeSpan.FromMinutes(5)
            },
            NullLogger<AuditOutboxProcessor>.Instance);
    }

    private static AuditOutboxProcessingItem CreateItem(
        int attempts = 0,
        IReadOnlyDictionary<string, object?>? payloadOverrides = null)
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.Parse("cf9be753-83ad-4717-b4ab-c7e60255aa23");
        var correlationId = Guid.Parse("216c43d8-4d47-4313-a61f-b9cf5ecf1594");
        var entityId = Guid.Parse("c04f62de-e421-403f-a0f9-f24452f9ec58");
        var payload = new Dictionary<string, object?>
        {
            ["TenantId"] = tenantId,
            ["CorrelationId"] = correlationId,
            ["RequestType"] = "TestAuditableCommand",
            ["ActorType"] = AuditActorType.PlatformAdministrator.ToString(),
            ["ActorId"] = Guid.Parse("2f5a8c18-5361-402d-bc6d-40575d48250c"),
            ["ActorEmailMasked"] = "a***@diten.com",
            ["ActorDisplayNameMasked"] = "A***n",
            ["TargetTenantId"] = tenantId,
            ["Category"] = AuditCategory.Security.ToString(),
            ["EntityType"] = "TestEntity",
            ["EntityId"] = entityId,
            ["Operation"] = AuditOperation.Update.ToString(),
            ["Outcome"] = AuditOutcome.Succeeded.ToString(),
            ["BeforeState"] = new Dictionary<string, object?> { ["name"] = "before" },
            ["AfterState"] = new Dictionary<string, object?> { ["name"] = "after" },
            ["Metadata"] = new Dictionary<string, object?> { ["source"] = "unit-test" },
            ["IpAddressMasked"] = "127.0.0.0",
            ["UserAgent"] = "xunit",
            ["OccurredAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1),
            ["SourceService"] = "Diten.Platform",
            ["SourceModule"] = "AuditWorkerTests",
            ["IsMetaAudit"] = false,
            ["RedactionStatus"] = AuditRedactionStatus.None.ToString()
        };

        if (payloadOverrides is not null)
        {
            foreach (var pair in payloadOverrides)
            {
                payload[pair.Key] = pair.Value;
            }
        }

        return new AuditOutboxProcessingItem(
            id,
            tenantId,
            correlationId,
            $"audit:{id:N}",
            "TestAuditableCommand",
            AuditOperation.Update,
            "TestEntity",
            entityId,
            payload,
            AuditOutboxStatus.Processing,
            attempts,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-2));
    }

    private sealed class FakeAuditOutboxProcessingRepository : IAuditOutboxProcessingRepository
    {
        private readonly Queue<AuditOutboxProcessingItem> _items;

        public FakeAuditOutboxProcessingRepository(params AuditOutboxProcessingItem[] items)
        {
            _items = new Queue<AuditOutboxProcessingItem>(items);
        }

        public List<Guid> CompletedIds { get; } = [];
        public List<FailureRecord> Failures { get; } = [];

        public Task<IReadOnlyList<AuditOutboxProcessingItem>> ClaimNextBatchAsync(
            int batchSize,
            int maxAttempts,
            DateTimeOffset now,
            TimeSpan processingStaleAfter,
            CancellationToken ct = default)
        {
            var claimed = new List<AuditOutboxProcessingItem>();
            while (claimed.Count < batchSize && _items.Count > 0)
            {
                claimed.Add(_items.Dequeue());
            }

            return Task.FromResult(claimed as IReadOnlyList<AuditOutboxProcessingItem>);
        }

        public Task MarkCompletedAsync(Guid id, CancellationToken ct = default)
        {
            CompletedIds.Add(id);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid id,
            AuditOutboxStatus status,
            int attempts,
            DateTimeOffset nextAttemptAtUtc,
            string lastError,
            CancellationToken ct = default)
        {
            Failures.Add(new FailureRecord(id, status, attempts, nextAttemptAtUtc, lastError));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        private readonly List<AuditEvent> _existingEvents;

        public FakeAuditEventRepository(params AuditEvent[] existingEvents)
        {
            _existingEvents = existingEvents.ToList();
        }

        public List<AuditEvent> Appended { get; } = [];
        public Exception? AppendException { get; init; }

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
        {
            if (AppendException is not null)
            {
                throw AppendException;
            }

            auditEvent.ValidateAppend();
            Appended.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult<AuditEvent?>(null);
        }

        public Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            return GetByIdForPlatformCrossTenantAsync(id, ct);
        }

        public Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid id, CancellationToken ct = default)
        {
            var auditEvent = _existingEvents.FirstOrDefault(item => item.Id == id);
            return Task.FromResult(auditEvent);
        }

        public Task<AuditEventSearchResult> SearchForPlatformCrossTenantAsync(AuditEventSearchRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new AuditEventSearchResult(_existingEvents, _existingEvents.Count));
        }

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default)
        {
            var events = _existingEvents.Where(auditEvent => auditEvent.CorrelationId == correlationId).ToList();
            return Task.FromResult(events as IReadOnlyList<AuditEvent>);
        }

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdForPlatformCrossTenantAsync(Guid correlationId, CancellationToken ct = default)
        {
            return GetByCorrelationIdAsync(correlationId, ct);
        }

        public Task<int> RedactActorPiiForPlatformCrossTenantAsync(AuditActorPiiRedactionRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed record FailureRecord(
        Guid Id,
        AuditOutboxStatus Status,
        int Attempts,
        DateTimeOffset NextAttemptAtUtc,
        string LastError);
}
