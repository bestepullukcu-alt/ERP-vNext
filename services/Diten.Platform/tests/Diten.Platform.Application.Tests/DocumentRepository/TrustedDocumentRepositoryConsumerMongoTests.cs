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
