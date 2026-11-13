using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Domain.Enums;
using Xunit;
using Host = Diten.Platform.Application.Tests.DocumentRepository.TrustedDocumentRepositoryConsumerHost;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU08 DL-1 — the USER path upload (<c>POST api/v1/document-repository/objects</c>, FU01 DL-1) on the real HTTP host
/// (TestServer, production access pipeline, real <c>AuditBehavior</c> + <c>AuditService</c>, test writer): AC-2 (the success
/// record names the stored object), AC-3 (its metadata is the pre-FU08 set — pinned on the code before FU08) and AC-10 (no
/// permission, no upload, no record).
/// </summary>
public sealed class DocumentRepositoryUserUploadAuditTests
{
    private const string UploadPath = "/api/v1/document-repository/objects";
    private static readonly Guid Tenant = Guid.Parse("26208080-0000-4000-8000-0000000000a1");

    private static Task<Host> StartAsync() => Host.StartAsync(new HostOptions());

    private static string Token(params string[] permissions) =>
        Host.UserToken(Tenant, Guid.NewGuid(), "tenant_user", permissions);

    private static Task<HttpResponseMessage> UploadAsync(Host host, string token, string body = "user path body") =>
        host.SendAsync(HttpMethod.Post, UploadPath, token, content: Form.Valid(Encoding.UTF8.GetBytes(body), "user.pdf"));

    private static Diten.Platform.Domain.Entities.Audit.AuditEvent SingleUploadRecord(Host host)
    {
        var write = Assert.Single(host.Audit.Writes, w => w.RequestType == nameof(StoreRepositoryObjectCommand));
        Assert.Equal(Tenant, write.TenantId);
        return InMemoryAuditOutbox.ToAuditEvent(write);
    }

    [Fact]
    public async Task A_user_upload_record_carries_the_content_id_and_the_after_state()
    {
        await using var host = await StartAsync();

        using var response = await UploadAsync(host, Token("platform.document-repository.manage"), "user path after-state");

        // DL-1 answers 200 (FU01 §19: the user path's Response.Success default; unchanged by FU08).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentId = await Host.ContentIdAsync(response);
        var row = Assert.Single(host.Options.Store.Rows);
        var record = SingleUploadRecord(host);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        Assert.Equal(contentId, record.EntityId);
        Assert.Equal(row.ContentId, record.EntityId);
        Assert.Equal(
            new[] { "ByteSize", "Checksum", "ContentId", "OwningItemId", "OwningVersionId", "Scope" },
            record.AfterState!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(contentId.ToString("D"), record.AfterState["ContentId"]?.ToString());
        Assert.Equal("Documents", record.AfterState["Scope"]?.ToString());
        Assert.Equal(row.OwningItemId.ToString("D"), record.AfterState["OwningItemId"]?.ToString());
        Assert.Equal(row.OwningVersionId.ToString("D"), record.AfterState["OwningVersionId"]?.ToString());
        Assert.Equal(row.ByteSize, Convert.ToInt64(record.AfterState["ByteSize"]));
        Assert.Equal(row.Checksum, record.AfterState["Checksum"]?.ToString());
        var payload = JsonSerializer.Serialize(record.AfterState);
        Assert.DoesNotContain(row.ObjectKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("user.pdf", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("application/pdf", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("local-filesystem", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_user_with_neither_manage_nor_visit_evidence_write_cannot_upload_and_no_upload_record_is_written()
    {
        await using var host = await StartAsync();

        using var response = await UploadAsync(host, Token("platform.document-repository.read"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(host.Options.Store.Rows);
        Assert.Equal(0, host.Storage.StoreCalls);
        Assert.DoesNotContain(host.Audit.Writes, w => w.RequestType == nameof(StoreRepositoryObjectCommand));
    }

    [Fact]
    public async Task A_user_upload_record_metadata_is_unchanged()
    {
        await using var host = await StartAsync();

        using var response = await UploadAsync(host, Token("platform.document-repository.manage"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var record = SingleUploadRecord(host);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        // Pinned on the code BEFORE FU08 (MVP-1 line 677a6a761): the command gives no metadata, so the record's own keys
        // are none. The outbox mapper's delivery keys (AuditOutbox*) are not the request's.
        Assert.Empty(TrustedDocumentRepositoryConsumerUploadTests.Keys(record));
    }
}
