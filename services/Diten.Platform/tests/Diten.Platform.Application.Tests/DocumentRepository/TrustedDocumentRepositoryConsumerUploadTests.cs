using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Handlers.QueryHandlers;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Host = Diten.Platform.Application.Tests.DocumentRepository.TrustedDocumentRepositoryConsumerHost;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 — upload, visibility and the records of a successful chain on the REAL HTTP host: AC-18, AC-20,
/// AC-21, AC-22 (in-memory rows; the Mongo round trip is in the Mongo tests), AC-23, AC-24, AC-25, AC-26 (authenticated
/// rows), AC-27 (streams outside the budget), AC-30, AC-32, AC-34, AC-35 (a)–(d), (f).
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerUploadTests
{
    private static readonly Guid Client = TrustedDocumentRepositoryConsumerGateTests.Client;
    private static readonly Guid TenantA = TrustedDocumentRepositoryConsumerGateTests.TenantA;
    private static readonly Guid TenantB = Guid.Parse("26202620-0000-4000-8000-0000000000b1");

    private static Task<Host> StartAsync(InMemoryObjectStore? store = null) =>
        Host.StartAsync(new HostOptions { Entries = [Host.Entry(Client)], Store = store ?? new InMemoryObjectStore() });

    private static string Token(Guid? tenant = null) => Host.ServiceToken(Client, tenant ?? TenantA);

    /// <summary>The record's own metadata keys; the outbox mapper's delivery keys (AuditOutbox*) are not the request's.</summary>
    internal static IEnumerable<string> Keys(Diten.Platform.Domain.Entities.Audit.AuditEvent record) =>
        record.Metadata.Keys.Where(k => !k.StartsWith("AuditOutbox", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

    private static byte[] Bytes(long size, byte fill = 0x41)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, fill);
        return bytes;
    }

    // ── AC-24 / AC-25 — a service token without any permission runs the whole chain; every success is ONE record.

    [Fact]
    public async Task A_service_token_without_permissions_runs_upload_meta_content_compensate_and_each_success_is_one_service_record()
    {
        await using var host = await StartAsync();
        var correlation = Guid.NewGuid();
        void Correlate(HttpRequestMessage r) => r.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation.ToString("D"));

        using var upload = await host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("contract body")), Correlate);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var contentId = await Host.ContentIdAsync(upload);
        Assert.DoesNotContain("objectKey", await upload.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var meta = await host.GetAsync(Token(), contentId, Correlate);
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        Assert.Single(host.Audit.Writes); // a successful metadata read writes nothing

        using var content = await host.ContentAsync(Token(), contentId, Correlate);
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("contract body", await content.Content.ReadAsStringAsync());

        using var compensate = await host.CompensateAsync(Token(), contentId, Correlate);
        Assert.Equal(HttpStatusCode.NoContent, compensate.StatusCode);

        var records = host.Records();
        Assert.Equal(3, records.Count);
        Assert.All(host.Audit.Writes, w => Assert.Equal(TenantA, w.TenantId));
        Assert.All(records, r =>
        {
            Assert.Equal(AuditActorType.Service, r.ActorType);
            Assert.Equal(Client, r.ActorId);
            Assert.Null(r.ActorEmailMasked);
            Assert.Equal("Diten.Platform", r.SourceService);
            Assert.Equal("RepositoryObject", r.EntityType);
            Assert.Equal(AuditOutcome.Succeeded, r.Outcome);
            Assert.Equal(correlation, r.CorrelationId);
            Assert.Equal(Client.ToString("D"), r.Metadata["serviceClientId"]?.ToString());
        });

        var common = new[] { "audience", "callerService", "consumerModule", "operation", "scope", "serviceClientId" };
        var store = records.Single(r => r.RequestType == nameof(StoreTrustedRepositoryObjectCommand));
        Assert.Equal(contentId, store.EntityId); // MOD-0262-FU08 AC-1
        Assert.Equal(common, Keys(store));
        Assert.Equal("store", store.Metadata["operation"]?.ToString());
        Assert.Equal(AuditOperation.Create, store.Operation);

        var read = records.Single(r => r.RequestType == OpenTrustedRepositoryObjectContentHandler.ContentReadRequestType);
        Assert.Equal(common.Append("byteSize").Append("delivery").Order(StringComparer.Ordinal), Keys(read));
        Assert.Equal("content-read", read.Metadata["operation"]?.ToString());
        Assert.Equal("started", read.Metadata["delivery"]?.ToString());
        Assert.Equal(contentId, read.EntityId);
        Assert.Equal(AuditOperation.Export, read.Operation);

        var compensation = records.Single(r => r.RequestType == nameof(CompensateTrustedRepositoryObjectCommand));
        Assert.Equal(common.Append("alreadyCompensated").Order(StringComparer.Ordinal), Keys(compensation));
        Assert.Equal("compensate", compensation.Metadata["operation"]?.ToString());
        Assert.Equal("False", compensation.Metadata["alreadyCompensated"]?.ToString());
        Assert.Equal(contentId, compensation.EntityId);
        Assert.All(records, r =>
        {
            Assert.Equal("Diten.MDM", r.Metadata["callerService"]?.ToString());
            Assert.Equal("MOD-0217", r.Metadata["consumerModule"]?.ToString());
            Assert.Equal(TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience, r.Metadata["audience"]?.ToString());
            Assert.Equal("Documents", r.Metadata["scope"]?.ToString());
        });
    }

    // ── AC-22 — the row is bound to the consumer MODULE; the client id is trace only.

    [Fact]
    public async Task The_upload_binds_the_row_to_the_consumer_module_and_records_the_client_as_trace_only()
    {
        await using var host = await StartAsync();

        var contentId = await host.UploadOkAsync(Token());

        var row = Assert.Single(host.Options.Store.Rows);
        Assert.Equal(contentId, row.ContentId);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal("MOD-0217", row.TrustedConsumerModule);
        Assert.Equal(Client, row.TrustedConsumerClientId);
        Assert.Equal($"service:Diten.MDM:{Client:D}", row.CreatedBy);
        Assert.Equal(Form.Company, row.CompanyId);
    }

    [Theory]
    [InlineData("user-path-row")]
    [InlineData("other-module-row")]
    public async Task A_row_the_consumer_module_does_not_own_is_404_on_meta_content_and_compensate(string kind)
    {
        var store = new InMemoryObjectStore();
        await using var host = await StartAsync(store);
        var contentId = await host.UploadOkAsync(Token());
        var row = store.Rows.Single();
        row.TrustedConsumerModule = kind == "user-path-row" ? null : "MOD-0999";
        row.TrustedConsumerClientId = kind == "user-path-row" ? null : row.TrustedConsumerClientId;

        foreach (var call in new Func<Task<HttpResponseMessage>>[]
                 {
                     () => host.GetAsync(Token(), contentId),
                     () => host.ContentAsync(Token(), contentId),
                     () => host.CompensateAsync(Token(), contentId)
                 })
        {
            using var response = await call();
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(DocumentRepositoryReasonCodes.NotFoundNonLeakage, await Host.ReasonCodeAsync(response));
        }

        Assert.False(row.IsDeleted);
        Assert.Empty(host.Storage.DeleteTokens);
        var refusals = host.Records().Where(r => r.RequestType == TrustedDocumentRepositoryRefusalAudit.DeniedRequestType).ToList();
        Assert.Equal(3, refusals.Count); // one per call, each by the refusal writer (AuditBehavior writes none)
        Assert.All(refusals, r => Assert.Equal(DocumentRepositoryReasonCodes.NotFoundNonLeakage, r.Metadata["ReasonCode"]?.ToString()));
    }

    [Fact]
    public async Task A_renewed_client_of_the_same_module_reaches_the_old_clients_objects_and_the_old_token_is_403()
    {
        var store = new InMemoryObjectStore();
        var root = Path.Combine(Path.GetTempPath(), "DitenDl2Trusted", Guid.NewGuid().ToString("N"));
        var clientB = Guid.NewGuid();
        Guid contentId;
        // Both hosts share the rows and the storage root; the first stays up (its disposal empties the root).
        await using var before = await Host.StartAsync(new HostOptions { Entries = [Host.Entry(Client)], Store = store, Root = root });
        contentId = await before.UploadOkAsync(Token(), "renewal body");

        await using var after = await Host.StartAsync(new HostOptions { Entries = [Host.Entry(clientB)], Store = store, Root = root });
        var tokenB = Host.ServiceToken(clientB, TenantA);

        using (var meta = await after.GetAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.OK, meta.StatusCode); }
        using (var content = await after.ContentAsync(tokenB, contentId))
        {
            Assert.Equal(HttpStatusCode.OK, content.StatusCode);
            Assert.Equal("renewal body", await content.Content.ReadAsStringAsync());
        }

        using (var old = await after.GetAsync(Token(), contentId)) { Assert.Equal(HttpStatusCode.Forbidden, old.StatusCode); }
        using (var compensate = await after.CompensateAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.NoContent, compensate.StatusCode); }
    }

    // ── AC-18 — tenant only from the token.

    [Fact]
    public async Task Another_tenants_token_gets_404_on_meta_content_and_compensate_and_the_object_is_untouched()
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token(TenantA));
        var tokenB = Token(TenantB);

        using (var meta = await host.GetAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.NotFound, meta.StatusCode); }
        using (var content = await host.ContentAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.NotFound, content.StatusCode); }
        using (var compensate = await host.CompensateAsync(tokenB, contentId)) { Assert.Equal(HttpStatusCode.NotFound, compensate.StatusCode); }

        var row = Assert.Single(host.Options.Store.Rows);
        Assert.False(row.IsDeleted);
        Assert.Empty(host.Storage.DeleteTokens);
        using var stillThere = await host.ContentAsync(Token(TenantA), contentId);
        Assert.Equal("contract body", await stillThere.Content.ReadAsStringAsync());
        Assert.Equal(3, host.Audit.Writes.Count(w => w.TenantId == TenantB));
    }

    [Fact]
    public async Task A_tenantId_form_field_is_400_and_never_chooses_the_tenant()
    {
        await using var host = await StartAsync();
        var parts = Form.Fields().Prepend(("tenantId", (object)TenantB.ToString("D"))).Append(Form.File(Encoding.UTF8.GetBytes("x"))).ToArray();

        using var response = await host.UploadAsync(Token(), Form.Parts(parts));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Storage.StoreCalls);
        Assert.Empty(host.Options.Store.Rows);
    }

    // ── AC-20 — streaming, never the framework's form.

    [Fact]
    public void The_trusted_controller_binds_no_form_and_the_executor_reads_with_MultipartReader()
    {
        var actions = typeof(InternalTrustedDocumentRepositoryConsumerController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(actions);
        foreach (var parameter in actions.SelectMany(a => a.GetParameters()))
        {
            Assert.False(typeof(IFormFile).IsAssignableFrom(parameter.ParameterType), parameter.Name);
            Assert.False(typeof(IFormCollection).IsAssignableFrom(parameter.ParameterType), parameter.Name);
            Assert.Null(parameter.GetCustomAttribute<FromFormAttribute>());
        }

        var controllerSource = File.ReadAllText(Path.Combine(RepoPaths.Root(), 
            "services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedDocumentRepositoryConsumerController.cs"));
        var executorSource = File.ReadAllText(Path.Combine(RepoPaths.Root(), 
            "services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs"));
        foreach (var source in new[] { controllerSource, executorSource })
        {
            Assert.DoesNotContain("ReadFormAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IFormFile", source, StringComparison.Ordinal);
            Assert.DoesNotContain("[FromForm", source, StringComparison.Ordinal);
        }

        Assert.Contains("new MultipartReader(", executorSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_upload_succeeds_when_reading_the_framework_form_would_throw()
    {
        await using var host = await Host.StartAsync(new HostOptions
        {
            Entries = [Host.Entry(Client)],
            BeforePipeline = app => app.Use(async (context, next) =>
            {
                context.Features.Set<IFormFeature>(new ThrowingFormFeature());
                await next();
            })
        });

        using var response = await host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("streamed")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── AC-21 / AC-34 — the multipart contract and its boundary values: 400, no provider call, no row.

    public static TheoryData<string> BrokenForms => new()
    {
        "missing-field-before-file-then-field", "missing-companyId", "empty-owningItemId", "repeated-scope", "unknown-field", "two-files",
        "no-boundary", "scope-documents", "scope-0", "scope-space", "guid-N", "guid-blank"
    };

    [Theory]
    [MemberData(nameof(BrokenForms))]
    public async Task A_form_outside_the_contract_is_400_and_leaves_no_row(string fixture)
    {
        await using var host = await StartAsync();
        var file = Form.File(Encoding.UTF8.GetBytes("payload"));
        var fields = Form.Fields().ToList();
        (string, object) Field(string name, string value) => (name, value);
        HttpContent content = fixture switch
        {
            "missing-field-before-file-then-field" => Form.Parts(fields.Take(3).Append(file).Append(fields[3]).ToArray()),
            "missing-companyId" => Form.Parts(fields.Where(f => f.Name != "companyId").Append(file).ToArray()),
            "empty-owningItemId" => Form.Parts(fields.Select(f => f.Name == "owningItemId" ? Field(f.Name, Guid.Empty.ToString("D")) : f).Append(file).ToArray()),
            "repeated-scope" => Form.Parts(fields.Append(Field("scope", "Documents")).Append(file).ToArray()),
            "unknown-field" => Form.Parts(fields.Append(Field("note", "x")).Append(file).ToArray()),
            "two-files" => Form.Parts(fields.Append(file).Append(Form.File(Encoding.UTF8.GetBytes("second"), "second.pdf")).ToArray()),
            "no-boundary" => new StringContent("x", Encoding.UTF8, "multipart/form-data"),
            "scope-documents" => Form.Valid(Encoding.UTF8.GetBytes("x"), scope: "documents"),
            "scope-0" => Form.Valid(Encoding.UTF8.GetBytes("x"), scope: "0"),
            "scope-space" => Form.Valid(Encoding.UTF8.GetBytes("x"), scope: " Documents"),
            "guid-N" => Form.Parts(fields.Select(f => f.Name == "companyId" ? Field(f.Name, Form.Company.ToString("N")) : f).Append(file).ToArray()),
            _ => Form.Parts(fields.Select(f => f.Name == "owningVersionId" ? Field(f.Name, string.Empty) : f).Append(file).ToArray())
        };
        if (fixture == "no-boundary")
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");
        }

        using var response = await host.UploadAsync(Token(), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.ValidationFailed, await Host.ReasonCodeAsync(response));
        Assert.Empty(host.Options.Store.Rows);
        Assert.Empty(Directory.Exists(host.Root) ? Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories) : []);
        Assert.Single(host.Audit.Writes);
        // Streaming cannot see a trailing part before the file is copied (DL-2 developer decision, reported): those two
        // reach the provider once and leave nothing; every other fixture is refused before the provider.
        Assert.Equal(fixture is "two-files" ? 1 : 0, host.Storage.StoreCalls);
    }

    [Fact]
    public async Task Form_fields_over_16_KiB_together_are_400()
    {
        await using var host = await StartAsync();
        var fields = Form.Fields().ToList();
        fields[0] = (fields[0].Name, Form.OwningItem.ToString("D") + new string(' ', 16 * 1024));

        using var response = await host.UploadAsync(Token(), Form.Parts(fields.Append(Form.File(Encoding.UTF8.GetBytes("x"))).ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Storage.StoreCalls);
    }

    [Fact]
    public async Task A_validator_refusal_is_a_bodied_400_written_once_by_the_refusal_writer()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("x"), new string('a', 300) + ".pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.ValidationFailed, await Host.ReasonCodeAsync(response));
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, host.Storage.StoreCalls);
        var record = Assert.Single(host.Records());
        Assert.Equal(TrustedDocumentRepositoryRefusalAudit.DeniedRequestType, record.RequestType);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal(AuditActorType.Service, record.ActorType);
    }

    // ── AC-23 / K-C — the user path never sees a consumer's row.

    [Fact]
    public async Task The_user_path_answers_404_for_a_consumer_row_even_with_read_and_manage()
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());
        var user = Host.UserToken(TenantA, Guid.NewGuid(), "tenant_user",
            "platform.document-repository.read", "platform.document-repository.manage");

        foreach (var (method, path) in new[]
                 {
                     (HttpMethod.Get, $"/api/v1/document-repository/objects/{contentId:D}"),
                     (HttpMethod.Get, $"/api/v1/document-repository/objects/{contentId:D}/content"),
                     (HttpMethod.Post, $"/api/v1/document-repository/objects/{contentId:D}/compensate")
                 })
        {
            using var response = await host.SendAsync(method, path, user);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.False(host.Options.Store.Rows.Single().IsDeleted);
    }

    // ── AC-27 — the copy and the stream are outside the 2 s budget.

    [Fact]
    public async Task A_five_MiB_upload_arriving_over_three_seconds_and_a_three_second_read_both_succeed()
    {
        await using var host = await StartAsync();
        var slow = new SlowContent(5 * 1024 * 1024, TimeSpan.FromSeconds(3));

        using var upload = await host.UploadAsync(Token(), Form.Parts(Form.Fields().Append(Form.File(slow)).ToArray()));

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var contentId = await Host.ContentIdAsync(upload);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Host.BasePath}/{contentId:D}/content");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token());
        using var response = await host.Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[1024 * 1024];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            total += read;
            await Task.Delay(600);
        }

        Assert.Equal(5 * 1024 * 1024, total);
    }

    // ── AC-32 — the one trusted limit.

    [Fact]
    public async Task A_file_of_exactly_50_MiB_is_201()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Valid(Bytes(TrustedDocumentRepositoryUploadLimits.MaxFileBytes)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(host.Options.Store.Rows);
    }

    [Fact]
    public async Task One_byte_over_50_MiB_is_413_from_inside_the_command_with_one_AuditBehavior_record_and_no_partial_file()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Valid(Bytes(TrustedDocumentRepositoryUploadLimits.MaxFileBytes + 1)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerTooLarge, await Host.ReasonCodeAsync(response));
        Assert.Empty(host.Options.Store.Rows);
        Assert.Empty(Directory.Exists(host.Root) ? Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories) : []);
        var record = Assert.Single(host.Records()); // AC-35 (a): AuditBehavior alone, 413 not 503
        Assert.Equal(nameof(StoreTrustedRepositoryObjectCommand), record.RequestType);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal("413", record.Metadata["ResponseStatusCode"]?.ToString());
        Assert.Null(record.EntityId);   // MOD-0262-FU08 AC-4
        Assert.Null(record.AfterState);
    }

    [Fact]
    public async Task The_upload_endpoint_declares_the_trusted_request_limit_in_its_metadata()
    {
        await using var host = await StartAsync();
        var endpoint = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "api/internal/v1/document-repository/trusted-consumer/objects"
                         && e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

        Assert.Equal(TrustedDocumentRepositoryUploadLimits.MaxRequestBytes, endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        Assert.Equal(53_477_376, TrustedDocumentRepositoryUploadLimits.MaxRequestBytes);
    }

    // ── AC-35 — outcomes inside the command, one writer each.

    [Fact]
    public async Task A_provider_400_is_one_AuditBehavior_record()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("x"), "macro.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var record = Assert.Single(host.Records());
        Assert.Equal(nameof(StoreTrustedRepositoryObjectCommand), record.RequestType);
        Assert.Equal("400", record.Metadata["ResponseStatusCode"]?.ToString());
        Assert.Null(record.EntityId);   // MOD-0262-FU08 AC-4
        Assert.Null(record.AfterState);
    }

    [Fact]
    public async Task A_body_cut_without_cancellation_is_503_with_one_AuditBehavior_record_and_nothing_left()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Valid(Bytes(3 * 1024 * 1024)),
            r => r.Headers.TryAddWithoutValidation(TestBodyFaults.Header, "io"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.StorageUnavailable, await Host.ReasonCodeAsync(response));
        Assert.Empty(host.Options.Store.Rows);
        Assert.Empty(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
        var record = Assert.Single(host.Records());
        Assert.Equal("503", record.Metadata["ResponseStatusCode"]?.ToString());
    }

    [Theory]
    [InlineData("abort")]    // (d) OperationCanceledException
    [InlineData("abort-io")] // (f) IOException → 503 by the provider, then the token decides
    public async Task A_client_abort_during_the_bytes_is_one_client_aborted_record_from_the_executor(string fault)
    {
        await using var host = await StartAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => host.UploadAsync(Token(), Form.Valid(Bytes(3 * 1024 * 1024)),
            r => r.Headers.TryAddWithoutValidation(TestBodyFaults.Header, fault)));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        var record = Assert.Single(host.Records());
        Assert.Equal(TrustedDocumentRepositoryRefusalAudit.ClientAbortedRequestType, record.RequestType);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerClientAborted, record.Metadata["ReasonCode"]?.ToString());
        Assert.Equal(AuditOperation.Create, record.Operation);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal(AuditActorType.Service, record.ActorType);
        Assert.Equal(Client, record.ActorId);
        Assert.Equal(TenantA, host.Audit.Writes.Single().TenantId);
        Assert.Empty(host.Options.Store.Rows);
        Assert.Empty(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
        if (fault == "abort")
        {
            Assert.Equal(1, host.Logs.Count(Microsoft.Extensions.Logging.LogLevel.Error, "Unhandled exception for request"));
        }
    }

    // ── MOD-0262-FU08 — the upload record names the stored object (AC-1, AC-8); a part after a COMPLETE file (AC-11, AC-12).

    [Fact]
    public async Task A_trusted_upload_record_carries_the_content_id_and_the_after_state()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("after-state body")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contentId = await Host.ContentIdAsync(response);
        var row = Assert.Single(host.Options.Store.Rows);
        var record = Assert.Single(host.Records(), r => r.RequestType == nameof(StoreTrustedRepositoryObjectCommand));
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        Assert.Equal(contentId, record.EntityId);
        Assert.Equal(
            new[] { "ByteSize", "Checksum", "ContentId", "OwningItemId", "OwningVersionId", "Scope", "TrustedConsumerModule" },
            record.AfterState!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(contentId.ToString("D"), record.AfterState["ContentId"]?.ToString());
        Assert.Equal("Documents", record.AfterState["Scope"]?.ToString());
        Assert.Equal(row.OwningItemId.ToString("D"), record.AfterState["OwningItemId"]?.ToString());
        Assert.Equal(row.OwningVersionId.ToString("D"), record.AfterState["OwningVersionId"]?.ToString());
        Assert.Equal(row.ByteSize, Convert.ToInt64(record.AfterState["ByteSize"]));
        Assert.Equal(row.Checksum, record.AfterState["Checksum"]?.ToString());
        Assert.Equal("MOD-0217", record.AfterState["TrustedConsumerModule"]?.ToString());
        var written = System.Text.Json.JsonSerializer.Serialize(new { record.AfterState, record.Metadata });
        foreach (var forbidden in new[] { row.ObjectKey, row.FileName, row.MediaType, row.StorageProvider, row.CreatedBy, "after-state body" })
        {
            Assert.DoesNotContain(forbidden, written, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Two_tenants_uploads_each_record_names_its_own_tenants_content_id()
    {
        await using var host = await StartAsync();

        using var a = await host.UploadAsync(Token(TenantA), Form.Valid(Encoding.UTF8.GetBytes("tenant A")));
        using var b = await host.UploadAsync(Token(TenantB), Form.Valid(Encoding.UTF8.GetBytes("tenant B")));
        var contentA = await Host.ContentIdAsync(a);
        var contentB = await Host.ContentIdAsync(b);

        var writes = host.Audit.Writes.Where(w => w.RequestType == nameof(StoreTrustedRepositoryObjectCommand)).ToList();
        Assert.Equal(2, writes.Count);
        var writeA = Assert.Single(writes, w => w.TenantId == TenantA);
        var writeB = Assert.Single(writes, w => w.TenantId == TenantB);
        Assert.Equal(contentA, InMemoryAuditOutbox.ToAuditEvent(writeA).EntityId);
        Assert.Equal(contentB, InMemoryAuditOutbox.ToAuditEvent(writeB).EntityId);
        Assert.Equal(contentA, host.Options.Store.Rows.Single(r => r.TenantId == TenantA).ContentId);
        Assert.Equal(contentB, host.Options.Store.Rows.Single(r => r.TenantId == TenantB).ContentId);
        Assert.DoesNotContain(contentA.ToString("D"), System.Text.Json.JsonSerializer.Serialize(writeB.Payload), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("four-fields-file-then-unknown-field")]
    [InlineData("four-fields-file-then-repeated-scope")]
    public async Task A_field_after_a_complete_file_is_400_with_one_AuditBehavior_record_and_nothing_left(string fixture)
    {
        await using var host = await StartAsync();
        (string, object) after = fixture == "four-fields-file-then-unknown-field" ? ("note", "x") : ("scope", "Documents");

        using var response = await host.UploadAsync(Token(),
            Form.Parts(Form.Fields().Append(Form.File(Encoding.UTF8.GetBytes("complete file"))).Append(after).ToArray()));

        await AssertTrailingPartRefusedAsync(host, response);
    }

    [Fact]
    public async Task Two_file_sections_is_400_with_one_AuditBehavior_record()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Token(), Form.Parts(Form.Fields()
            .Append(Form.File(Encoding.UTF8.GetBytes("first"), "first.pdf"))
            .Append(Form.File(Encoding.UTF8.GetBytes("second"), "second.pdf")).ToArray()));

        await AssertTrailingPartRefusedAsync(host, response);
    }

    private static async Task AssertTrailingPartRefusedAsync(Host host, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.ValidationFailed, await Host.ReasonCodeAsync(response));
        Assert.Equal(1, host.Storage.StoreCalls); // the fixture passed the whole file before the trailing part
        Assert.Empty(host.Options.Store.Rows);
        Assert.Empty(Directory.Exists(host.Root) ? Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories) : []);
        var record = Assert.Single(host.Records());
        Assert.Equal(nameof(StoreTrustedRepositoryObjectCommand), record.RequestType);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal(AuditOperation.Create, record.Operation);
        Assert.Equal("400", record.Metadata["ResponseStatusCode"]?.ToString());
        Assert.Null(record.EntityId);
        Assert.Null(record.AfterState);
        Assert.DoesNotContain(host.Records(), r => r.RequestType == TrustedDocumentRepositoryRefusalAudit.DeniedRequestType);
    }

    // ── AC-30 — compensation only.

    [Fact]
    public void The_trusted_controller_has_no_purge_destroy_or_delete_action()
    {
        var names = typeof(InternalTrustedDocumentRepositoryConsumerController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(new[] { "Compensate", "Content", "Get", "Store" }, names.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Purge", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Destroy", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.Null(typeof(InternalTrustedDocumentRepositoryConsumerController).GetMethods().SingleOrDefault(m => m.GetCustomAttribute<HttpDeleteAttribute>() is not null));
    }

    private sealed class ThrowingFormFeature : IFormFeature
    {
        public bool HasFormContentType => throw new InvalidOperationException("form read (test)");
        public IFormCollection? Form { get => throw new InvalidOperationException("form read (test)"); set { } }
        public IFormCollection ReadForm() => throw new InvalidOperationException("form read (test)");
        public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("form read (test)");
    }

    /// <summary>Writes <c>size</c> bytes in ten chunks spread over <c>duration</c>.</summary>
    private sealed class SlowContent(int size, TimeSpan duration) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            var chunk = new byte[size / 10];
            Array.Fill(chunk, (byte)0x42);
            for (var i = 0; i < 10; i++)
            {
                await stream.WriteAsync(chunk);
                await stream.FlushAsync();
                await Task.Delay(duration / 10);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = size;
            return true;
        }
    }
}
