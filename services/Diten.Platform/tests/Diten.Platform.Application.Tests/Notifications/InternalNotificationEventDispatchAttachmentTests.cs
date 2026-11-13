using System.Net;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-4) — files on the internal event dispatch, over real HTTP: a PDF (inline or a MOD-0262
/// content id of the SAME tenant) rides with every language group's mail; the set is bounded by the persisted-attachment
/// budget (so the retry keeps it — see <c>NotificationDispatchRetryFidelityTests</c>): above it 400 and nothing is sent;
/// a disallowed type, a path in the name, two sources, bad base64, another tenant's document → 400; the answer never echoes
/// a file.
/// </summary>
public sealed partial class InternalNotificationEventDispatchTests
{
    private static string B64(int bytes, byte fill = 0x25) => Convert.ToBase64String(Enumerable.Repeat(fill, bytes).ToArray());

    [Fact]
    public async Task A_pdf_within_the_budget_rides_with_every_language_groups_mail_and_the_answer_echoes_no_file()
    {
        using var host = new Host();
        var pdf = B64(1024);

        var response = await host.PostAsync(Body(
            to: [LineManagerOf(Rep), new InternalEventRecipient(UserId: EnglishColleague)],
            attachments: [new InternalEventAttachment("MG-ORD-000042.pdf", "application/pdf", ContentBase64: pdf)]));
        var raw = await response.Content.ReadAsStringAsync();
        var result = await host.ReadAsync(response, raw);

        Assert.Equal(2, result.Dispatches.Count);
        Assert.All(host.Adapter.Requests, r =>
        {
            var file = Assert.Single(r.Attachments!);
            Assert.Equal(("MG-ORD-000042.pdf", "application/pdf", 1024), (file.FileName, file.ContentType, file.Content.Length));
        });
        Assert.DoesNotContain(pdf[..40], raw);
        Assert.DoesNotContain("MG-ORD-000042.pdf", raw);
    }

    [Fact]
    public async Task Exactly_the_budget_is_sent_one_byte_more_is_400_and_nothing_is_sent()
    {
        // the platform's persisted-attachment budget, measured as a NUMBER (not read back from the constant under test)
        const int budget = 256 * 1024;
        using var host = new Host();
        var half = budget / 2;
        var exact = await host.PostAsync(Body(to: [LineManagerOf(Rep)], attachments:
        [
            new InternalEventAttachment("a.pdf", "application/pdf", ContentBase64: B64(half)),
            new InternalEventAttachment("b.png", "image/png", ContentBase64: B64(budget - half))
        ]));
        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
        Assert.Equal(2, Assert.Single(host.Adapter.Requests).Attachments!.Count);

        using var over = new Host();
        var refused = await over.PostAsync(Body(to: [LineManagerOf(Rep)], attachments:
        [
            new InternalEventAttachment("a.pdf", "application/pdf", ContentBase64: B64(half)),
            new InternalEventAttachment("b.pdf", "application/pdf", ContentBase64: B64(budget - half + 1))
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(InternalEventDispatchService.ReasonBadRequest, await refused.Content.ReadAsStringAsync());
        Assert.Empty(over.Adapter.Requests);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("path")]
    [InlineData("two-sources")]
    [InlineData("no-source")]
    [InlineData("bad-base64")]
    [InlineData("too-many")]
    [InlineData("no-name")]
    public async Task A_malformed_attachment_is_400_and_nothing_is_sent(string shape)
    {
        using var host = new Host();
        var ok = B64(10);
        IReadOnlyList<InternalEventAttachment> attachments = shape switch
        {
            "type" => [new("x.exe", "application/x-msdownload", ContentBase64: ok)],
            "path" => [new("../x.pdf", "application/pdf", ContentBase64: ok)],
            "two-sources" => [new("x.pdf", "application/pdf", ContentBase64: ok, DocumentRef: Guid.NewGuid())],
            "no-source" => [new("x.pdf", "application/pdf")],
            "bad-base64" => [new("x.pdf", "application/pdf", ContentBase64: "not base64 !!")],
            "too-many" => Enumerable.Range(0, InternalEventDispatchService.MaxAttachments + 1).Select(i => new InternalEventAttachment($"{i}.pdf", "application/pdf", ContentBase64: ok)).ToList(),
            _ => [new(" ", "application/pdf", ContentBase64: ok)]
        };

        var response = await host.PostAsync(Body(to: [LineManagerOf(Rep)], attachments: attachments));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.Adapter.Requests);
    }

    [Fact]
    public async Task A_document_reference_is_read_in_the_bodys_tenant_only()
    {
        using var host = new Host();
        var mine = host.Files.Add(TenantA, "MG-ORD-000007.pdf", new byte[2048]);
        var foreign = host.Files.Add(TenantB, "foreign.pdf", new byte[16]);
        var big = host.Files.Add(TenantA, "big.pdf", new byte[InternalEventDispatchService.MaxAttachmentBytes + 1]);

        var ok = await host.PostAsync(Body(to: [LineManagerOf(Rep)], attachments: [new InternalEventAttachment("siparis.pdf", "application/pdf", DocumentRef: mine)]));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2048, Assert.Single(Assert.Single(host.Adapter.Requests).Attachments!).Content.Length);

        foreach (var refused in new[] { foreign, big, Guid.NewGuid() })
        {
            var before = host.Adapter.Requests.Count;
            var response = await host.PostAsync(Body(to: [LineManagerOf(Rep)], attachments: [new InternalEventAttachment("x.pdf", "application/pdf", DocumentRef: refused)]));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(InternalEventDispatchService.ReasonAttachmentInvalid, await response.Content.ReadAsStringAsync());
            Assert.Equal(before, host.Adapter.Requests.Count);
        }
    }

    [Fact]
    public async Task No_attachments_field_sends_exactly_as_before()
    {
        using var host = new Host();
        var response = await host.PostAsync(Body(to: [LineManagerOf(Rep)]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Assert.Single(host.Adapter.Requests).Attachments);
    }

    /// <summary>MOD-0262 in memory: the rows read the AMBIENT tenant (the execution filter); the bytes by object key.</summary>
    internal sealed class Documents : IContentStorageGateway
    {
        private readonly List<RepositoryObject> _rows = [];
        private readonly Dictionary<string, byte[]> _bytes = [];

        public Guid Add(Guid tenant, string fileName, byte[] content)
        {
            var row = new RepositoryObject
            {
                TenantId = tenant, ContentId = Guid.NewGuid(), StorageProvider = "memory", ObjectKey = Guid.NewGuid().ToString("N"),
                Scope = "documents", FileName = fileName, MediaType = "application/pdf", ByteSize = content.Length, Checksum = "x"
            };
            _rows.Add(row);
            _bytes[row.ObjectKey] = content;
            return row.ContentId;
        }

        public IRepositoryObjectRepository For(ITenantContext tenant) => new Scoped(this, tenant);

        public Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default)
            => Task.FromResult(_bytes.TryGetValue(objectKey, out var b)
                ? Response<ContentStreamResult>.Success(new ContentStreamResult(new MemoryStream(b), "application/pdf", "x", b.Length))
                : Response<ContentStreamResult>.Fail("missing", 404));

        public Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default) => throw new NotSupportedException();

        private sealed class Scoped(Documents owner, ITenantContext tenant) : IRepositoryObjectRepository
        {
            public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default)
                => Task.FromResult(owner._rows.FirstOrDefault(r => r.ContentId == contentId && r.TenantId == tenant.TenantId));

            public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default) => throw new NotSupportedException();
        }
    }
}
