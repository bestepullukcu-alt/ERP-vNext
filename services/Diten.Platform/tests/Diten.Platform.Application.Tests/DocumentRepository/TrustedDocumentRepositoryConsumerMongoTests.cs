using System.Net;
using System.Text;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;
using Host = Diten.Platform.Application.Tests.DocumentRepository.TrustedDocumentRepositoryConsumerHost;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 on a REAL MongoDB (<see cref="MongoIntegrationHarness"/>): the two new row fields round-trip (AC-22),
/// and — r8 D-2 / inceleme F-3 — the record count of the cancellation cases with the PRODUCTION audit writer
/// (<c>AuditOutboxRepository</c> → <c>audit_outbox</c>), as two separate cases:
/// <list type="number">
/// <item>(i) the client goes away BEFORE <c>AuditBehavior</c> writes (AC-33 (4) / AC-35 (g) as written): the production
/// writer gets a cancelled token.</item>
/// <item>(ii) the client goes away WHILE the success record's insert is in flight (the D-2 window).</item>
/// </list>
/// 1 or 2 is accepted, more than 2 is a defect. The count is printed for the development record.
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerMongoTests : IAsyncLifetime
{
    private static readonly Guid Client = TrustedDocumentRepositoryConsumerGateTests.Client;
    private readonly ITestOutputHelper _output;
    private MongoIntegrationHarness _harness = null!;

    public TrustedDocumentRepositoryConsumerMongoTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() =>
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.DocumentManagement, SchemaProfile.AccessGovernance);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private Task<Host> StartAsync(Func<IServiceProvider, IAuditOutboxWriter>? writer = null) => Host.StartAsync(new HostOptions
    {
        Entries = [Host.Entry(Client)],
        Objects = sp => new RepositoryObjectRepository(_harness.DbContext, sp.GetRequiredService<ITenantContext>()),
        AuditWriter = writer
    });

    private string Token() => Host.ServiceToken(Client, _harness.TenantId);

    private IMongoCollection<RepositoryObject> Rows =>
        _harness.Database.GetCollection<RepositoryObject>(PlatformCollections.DocumentRepositoryObjects);

    private async Task<long> OutboxCountAsync() =>
        await _harness.Database.GetCollection<BsonDocument>("audit_outbox")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(_harness.TenantId, GuidRepresentation.Standard)));

    private Func<IServiceProvider, IAuditOutboxWriter> Production(Func<AuditOutboxWriteRequest, bool>? abortDuringInsertOf = null) =>
        sp => new AbortingWriter(new AuditOutboxRepository(_harness.DbContext), sp.GetRequiredService<IHttpContextAccessor>(), abortDuringInsertOf);

    [Fact]
    public async Task The_two_new_fields_round_trip_on_Mongo_and_an_older_row_reads_them_as_null()
    {
        await using var host = await StartAsync();

        var contentId = await host.UploadOkAsync(Token());

        var row = await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == contentId).SingleAsync();
        Assert.Equal("MOD-0217", row.TrustedConsumerModule);
        Assert.Equal(Client, row.TrustedConsumerClientId);
        Assert.Equal($"service:Diten.MDM:{Client:D}", row.CreatedBy);
        Assert.Equal(1, await Rows.CountDocumentsAsync(r => r.TrustedConsumerClientId == Client && r.TenantId == _harness.TenantId));

        var older = new BsonDocument
        {
            ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["TenantId"] = new BsonBinaryData(_harness.TenantId, GuidRepresentation.Standard),
            ["ContentId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["StorageProvider"] = "local-filesystem",
            ["ObjectKey"] = "older/key",
            ["Scope"] = "Documents",
            ["FileName"] = "older.pdf",
            ["MediaType"] = "application/pdf",
            ["Checksum"] = "00",
            ["IsDeleted"] = false
        };
        await _harness.Database.GetCollection<BsonDocument>(PlatformCollections.DocumentRepositoryObjects).InsertOneAsync(older);
        var olderRow = await Rows.Find(Builders<RepositoryObject>.Filter.Eq("_id", older["_id"])).SingleAsync();
        Assert.Null(olderRow.TrustedConsumerModule);
        Assert.Null(olderRow.TrustedConsumerClientId);
    }

    [Fact]
    public async Task F3_i_compensation_abort_before_AuditBehavior_writes_production_writer_count()
    {
        await using var host = await StartAsync(Production());
        var contentId = await host.UploadOkAsync(Token());
        var before = await OutboxCountAsync();
        host.Hooks.BeforeMark = context => context?.Abort();

        await CallIgnoringAbortAsync(() => host.CompensateAsync(Token(), contentId, Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        var records = await OutboxCountAsync() - before;
        _output.WriteLine($"AC-33 (4) production writer (i) abort before AuditBehavior: {records} record(s)");
        Assert.InRange(records, 1, 2);
        Assert.True((await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == contentId).SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task F3_i_metadata_write_abort_before_AuditBehavior_writes_production_writer_count()
    {
        await using var host = await StartAsync(Production());
        var before = await OutboxCountAsync();
        host.Hooks.BeforeCreate = context => context?.Abort();

        await CallIgnoringAbortAsync(() => host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("landed")), Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        var records = await OutboxCountAsync() - before;
        _output.WriteLine($"AC-35 (g) production writer (i) abort before AuditBehavior: {records} record(s)");
        Assert.InRange(records, 1, 2);
        Assert.Equal(1, await Rows.CountDocumentsAsync(r => r.TenantId == _harness.TenantId));
    }

    [Fact]
    public async Task F3_ii_compensation_abort_while_the_success_insert_is_in_flight_production_writer_count()
    {
        await using var host = await StartAsync(Production(r => r.RequestType == nameof(CompensateTrustedRepositoryObjectCommand)));
        var contentId = await host.UploadOkAsync(Token());
        var before = await OutboxCountAsync();

        await CallIgnoringAbortAsync(() => host.CompensateAsync(Token(), contentId, Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        var records = await OutboxCountAsync() - before;
        _output.WriteLine($"AC-33 (4) production writer (ii) abort during insert: {records} record(s)");
        Assert.InRange(records, 1, 2);
    }

    [Fact]
    public async Task F3_ii_metadata_write_abort_while_the_success_insert_is_in_flight_production_writer_count()
    {
        await using var host = await StartAsync(Production(r => r.RequestType == nameof(StoreTrustedRepositoryObjectCommand)));
        var before = await OutboxCountAsync();

        await CallIgnoringAbortAsync(() => host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("landed")), Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        var records = await OutboxCountAsync() - before;
        _output.WriteLine($"AC-35 (g) production writer (ii) abort during insert: {records} record(s)");
        Assert.InRange(records, 1, 2);
        Assert.Equal(1, await Rows.CountDocumentsAsync(r => r.TenantId == _harness.TenantId));
    }

    // ── DL-2 FIX-01 (CT acceptance ORTA-1) — the tenant predicate of the PRODUCTION repository on a real Mongo, two
    //    tenants. The in-memory double of the host tests copies the rule; these two run the shipped query.

    [Fact]
    public async Task FIX01_another_tenant_cannot_compensate_or_read_tenant_As_objects_through_the_production_repository()
    {
        await using var tenantB = await MongoIntegrationHarness.CreateAsync(SchemaProfile.DocumentManagement, SchemaProfile.AccessGovernance);
        await using var host = await StartAsync();
        var tokenA = Token();
        var tokenB = Host.ServiceToken(Client, tenantB.TenantId);

        var compensated = await host.UploadOkAsync(tokenA, "compensated by A", "a-compensated.pdf");
        using (var first = await host.CompensateAsync(tokenA, compensated)) { Assert.Equal(HttpStatusCode.NoContent, first.StatusCode); }
        var live = await host.UploadOkAsync(tokenA, "live object of A", "a-live.pdf");
        var compensatedBefore = await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == compensated).SingleAsync();
        var liveBefore = await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == live).SingleAsync();
        var deletesBefore = host.Storage.DeleteTokens.Count;
        var liveFile = Path.Combine(host.Root, liveBefore.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(liveFile));

        // Tenant B, same consumer module, same content ids: compensation (the DL-2 read that also sees compensated rows),
        // metadata and content — every one 404, the non-leakage answer.
        foreach (var contentId in new[] { compensated, live })
        {
            using (var compensate = await host.CompensateAsync(tokenB, contentId))
            {
                Assert.Equal(HttpStatusCode.NotFound, compensate.StatusCode);
                Assert.Equal("NOT_FOUND_NON_LEAKAGE", await Host.ReasonCodeAsync(compensate));
            }

            using (var meta = await host.GetAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.NotFound, meta.StatusCode); }
            using (var content = await host.ContentAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.NotFound, content.StatusCode); }
        }

        // A's rows, binary and the provider are untouched; tenant B owns nothing.
        var compensatedAfter = await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == compensated).SingleAsync();
        var liveAfter = await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == live).SingleAsync();
        Assert.True(compensatedAfter.IsDeleted);
        Assert.Equal(compensatedBefore.UpdatedAt, compensatedAfter.UpdatedAt);
        Assert.False(liveAfter.IsDeleted);
        Assert.Equal(liveBefore.UpdatedAt, liveAfter.UpdatedAt);
        Assert.Equal(liveBefore.Checksum, liveAfter.Checksum);
        Assert.Equal(deletesBefore, host.Storage.DeleteTokens.Count);
        Assert.True(File.Exists(liveFile));
        Assert.Equal(0, await Rows.CountDocumentsAsync(r => r.TenantId == tenantB.TenantId));
        var writesB = host.Audit.Writes.Where(w => w.TenantId == tenantB.TenantId).ToList();
        Assert.DoesNotContain(writesB, w => w.RequestType == nameof(CompensateTrustedRepositoryObjectCommand)); // refused at the pre-read
        Assert.Equal(6, writesB.Count(w => w.RequestType == "document-repository.trusted-consumer.denied")); // one per call

        using var stillThere = await host.ContentAsync(tokenA, live);
        Assert.Equal("live object of A", await stillThere.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FIX01_the_production_read_that_includes_compensated_rows_is_bound_to_the_ambient_tenant()
    {
        await using var tenantB = await MongoIntegrationHarness.CreateAsync(SchemaProfile.DocumentManagement, SchemaProfile.AccessGovernance);
        var contentId = Guid.NewGuid();
        var tenantA = new TenantContext();
        tenantA.SetTenant(_harness.TenantId);
        var repositoryA = new RepositoryObjectRepository(_harness.DbContext, tenantA);
        await repositoryA.CreateAsync(new RepositoryObject
        {
            TenantId = _harness.TenantId,
            ContentId = contentId,
            StorageProvider = "local-filesystem",
            ObjectKey = $"fix01/{contentId:N}",
            Scope = "Documents",
            FileName = "a.pdf",
            MediaType = "application/pdf",
            Checksum = "00",
            TrustedConsumerModule = "MOD-0217",
            TrustedConsumerClientId = Client
        });
        Assert.True(await repositoryA.MarkCompensatedAsync(contentId));

        var contextB = new TenantContext();
        contextB.SetTenant(tenantB.TenantId);
        var repositoryB = new RepositoryObjectRepository(_harness.DbContext, contextB);

        Assert.Null(await repositoryB.GetByContentIdIncludingCompensatedAsync(contentId));
        var own = await repositoryA.GetByContentIdIncludingCompensatedAsync(contentId);
        Assert.NotNull(own);
        Assert.True(own!.IsDeleted);
        Assert.Equal(_harness.TenantId, own.TenantId);
    }

    // ── MOD-0262-FU08 — the upload record's EntityId / AfterState on the PRODUCTION outbox (AC-9) and the idempotency-key
    //    effect of a retry with the same correlation (AC-16, §8 item 5).

    private async Task<List<Diten.Platform.Infrastructure.Persistence.Models.AuditOutboxMessage>> UploadRecordsAsync() =>
        await _harness.Database.GetCollection<Diten.Platform.Infrastructure.Persistence.Models.AuditOutboxMessage>("audit_outbox")
            .Find(m => m.TenantId == _harness.TenantId && m.RequestType == nameof(StoreTrustedRepositoryObjectCommand))
            .ToListAsync();

    private static Diten.Platform.Domain.Entities.Audit.AuditEvent Delivered(Diten.Platform.Infrastructure.Persistence.Models.AuditOutboxMessage m) =>
        Diten.Platform.Application.Tests.Audit.TransactionOwnedAuditSourceServiceTests.Deliver(new AuditOutboxWriteRequest
        {
            TenantId = m.TenantId,
            CorrelationId = m.CorrelationId,
            IdempotencyKey = m.IdempotencyKey,
            RequestType = m.RequestType,
            Operation = m.Operation,
            EntityType = m.EntityType,
            EntityId = m.EntityId,
            Payload = m.Payload
        });

    [Fact]
    public async Task An_upload_record_with_after_state_round_trips_on_the_production_outbox()
    {
        await using var host = await StartAsync(sp => new AuditOutboxRepository(_harness.DbContext));

        var contentId = await host.UploadOkAsync(Token(), "round trip body");

        var message = Assert.Single(await UploadRecordsAsync());
        Assert.Equal(contentId, message.EntityId);
        var record = Delivered(message);
        var row = await Rows.Find(r => r.TenantId == _harness.TenantId && r.ContentId == contentId).SingleAsync();
        Assert.Equal(contentId, record.EntityId);
        Assert.Equal(
            new[] { "ByteSize", "Checksum", "ContentId", "OwningItemId", "OwningVersionId", "Scope", "TrustedConsumerModule" },
            record.AfterState!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(contentId.ToString("D"), record.AfterState["ContentId"]?.ToString());
        Assert.Equal(row.OwningItemId.ToString("D"), record.AfterState["OwningItemId"]?.ToString());
        Assert.Equal(row.OwningVersionId.ToString("D"), record.AfterState["OwningVersionId"]?.ToString());
        Assert.Equal(row.ByteSize, Convert.ToInt64(record.AfterState["ByteSize"]));
        Assert.Equal(row.Checksum, record.AfterState["Checksum"]?.ToString());
        Assert.Equal("Documents", record.AfterState["Scope"]?.ToString());
        Assert.Equal("MOD-0217", record.AfterState["TrustedConsumerModule"]?.ToString());
    }

    [Fact]
    public async Task A_retry_with_the_same_correlation_after_a_503_keeps_both_upload_records()
    {
        await using var host = await StartAsync(sp => new AuditOutboxRepository(_harness.DbContext));
        var correlation = Guid.NewGuid().ToString("D");
        void Correlate(HttpRequestMessage r) => r.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        var large = new byte[3 * 1024 * 1024];

        using (var first = await host.UploadAsync(Token(), Form.Valid(large), r =>
               {
                   Correlate(r);
                   r.Headers.TryAddWithoutValidation(TestBodyFaults.Header, "io");
               }))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        }

        using var second = await host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("retry body")), Correlate);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var contentId = await Host.ContentIdAsync(second);

        var messages = await UploadRecordsAsync();
        _output.WriteLine($"AC-16 production writer, same correlation 503 then 201: {messages.Count} upload record(s)");
        Assert.Equal(2, messages.Count);
        Assert.Equal(2, messages.Select(m => m.IdempotencyKey).Distinct(StringComparer.Ordinal).Count());
        var records = messages.Select(Delivered).ToList();
        var failed = Assert.Single(records, r => r.Outcome == Diten.Platform.Domain.Enums.AuditOutcome.Failed);
        var succeeded = Assert.Single(records, r => r.Outcome == Diten.Platform.Domain.Enums.AuditOutcome.Succeeded);
        Assert.Null(failed.EntityId);
        Assert.Equal(contentId, succeeded.EntityId);
        Assert.All(messages, m => Assert.Equal(Guid.Parse(correlation), m.CorrelationId));
    }

    private static void Signal(HttpRequestMessage request) =>
        request.Headers.TryAddWithoutValidation(TestBodyFaults.SignalHeader, "1");

    private static async Task CallIgnoringAbortAsync(Func<Task<HttpResponseMessage>> call)
    {
        try { using var response = await call(); }
        catch (Exception) { /* the server-side Abort() reaches the client as an exception */ }
    }

    /// <summary>
    /// The production writer; for the chosen record it starts the insert with the live request token and aborts the
    /// request while that insert is in flight (the D-2 window: the driver may already have sent it).
    /// </summary>
    private sealed class AbortingWriter(
        IAuditOutboxWriter inner, IHttpContextAccessor accessor, Func<AuditOutboxWriteRequest, bool>? abortDuringInsertOf)
        : IAuditOutboxWriter
    {
        public async Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
        {
            if (abortDuringInsertOf?.Invoke(request) != true)
            {
                return await inner.TryEnqueueAsync(request, ct);
            }

            var insert = inner.TryEnqueueAsync(request, ct);
            accessor.HttpContext?.Abort();
            return await insert;
        }
    }
}
