using System.Net;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;
using Xunit;
using Host = Diten.Platform.Application.Tests.DocumentRepository.TrustedDocumentRepositoryConsumerHost;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 — AC-33 (repeat-safe compensation, the three timing cases, cancellation around the decision) and
/// AC-35 (g) (cancellation during the metadata write), on the REAL HTTP host with the TEST audit writer in both forms
/// (r8 D-2: "total 1" is measured with the test writer; the production Mongo writer is in the Mongo tests). The user path
/// twins pin r8 D-1: the irreversible steps keep the request token there.
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerCompensationTests
{
    private static readonly Guid Client = TrustedDocumentRepositoryConsumerGateTests.Client;
    private static readonly Guid TenantA = TrustedDocumentRepositoryConsumerGateTests.TenantA;

    private static Task<Host> StartAsync() => Host.StartAsync(new HostOptions { Entries = [Host.Entry(Client)] });

    private static string Token(Guid? tenant = null) => Host.ServiceToken(Client, tenant ?? TenantA);

    private static string UserToken() => Host.UserToken(TenantA, Guid.NewGuid(), "tenant_user",
        "platform.document-repository.read", "platform.document-repository.manage");

    private static IEnumerable<Diten.Platform.Domain.Entities.Audit.AuditEvent> Compensations(Host host) =>
        host.Records().Where(r => r.RequestType == nameof(CompensateTrustedRepositoryObjectCommand));

    [Fact]
    public async Task Case1_and_2_the_second_sequential_compensation_is_204_without_a_second_delete_and_says_alreadyCompensated()
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());

        using (var first = await host.CompensateAsync(Token(), contentId)) { Assert.Equal(HttpStatusCode.NoContent, first.StatusCode); }
        using (var second = await host.CompensateAsync(Token(), contentId)) { Assert.Equal(HttpStatusCode.NoContent, second.StatusCode); }

        Assert.Single(host.Storage.DeleteTokens);
        Assert.True(host.Storage.DeleteResults.Single());
        Assert.Equal(new[] { "False", "True" }, Compensations(host).Select(r => r.Metadata["alreadyCompensated"]?.ToString()));
        Assert.Empty(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Case3_a_call_that_loses_the_mark_answers_204_does_not_delete_and_logs_the_lost_race()
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());
        host.Hooks.MarkResult = false; // the pre-read saw it live; another call won the atomic mark

        using var response = await host.CompensateAsync(Token(), contentId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(host.Storage.DeleteTokens);
        Assert.Equal("False", Compensations(host).Single().Metadata["alreadyCompensated"]?.ToString());
        Assert.Equal(1, host.Logs.Count(LogLevel.Warning, "compensation race lost"));
    }

    [Fact]
    public async Task Case3_user_path_twin_a_lost_mark_deletes_nothing_and_answers_204()
    {
        await using var host = await StartAsync();
        var contentId = await UploadOnUserPathAsync(host);
        host.Hooks.MarkResult = false;

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/document-repository/objects/{contentId:D}/compensate", UserToken());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(host.Storage.DeleteTokens);
    }

    [Fact]
    public async Task The_user_paths_second_compensation_stays_404()
    {
        await using var host = await StartAsync();
        var contentId = await UploadOnUserPathAsync(host);
        var path = $"/api/v1/document-repository/objects/{contentId:D}/compensate";

        using (var first = await host.SendAsync(HttpMethod.Post, path, UserToken())) { Assert.Equal(HttpStatusCode.NoContent, first.StatusCode); }
        using var second = await host.SendAsync(HttpMethod.Post, path, UserToken());

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Theory]
    [InlineData("other-module")]
    [InlineData("other-tenant")]
    public async Task A_compensated_row_of_another_module_or_tenant_is_404_from_the_pre_read_and_sends_no_command(string kind)
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());
        using (var first = await host.CompensateAsync(Token(), contentId)) { Assert.Equal(HttpStatusCode.NoContent, first.StatusCode); }
        if (kind == "other-module")
        {
            host.Options.Store.Rows.Single().TrustedConsumerModule = "MOD-0999";
        }

        var before = host.Audit.Writes.Count;
        using var response = await host.CompensateAsync(kind == "other-tenant" ? Token(Guid.NewGuid()) : Token(), contentId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var added = host.Records().Skip(before).ToList();
        var refusal = Assert.Single(added);
        Assert.Equal(TrustedDocumentRepositoryRefusalAudit.DeniedRequestType, refusal.RequestType);
        Assert.Single(Compensations(host)); // the first call's only
    }

    [Fact]
    public async Task Defence_a_handler_404_after_the_pre_read_is_one_AuditBehavior_record_and_no_refusal_record()
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());
        var before = host.Audit.Writes.Count;
        host.Hooks.BeforeIncludingCompensatedRead = (n, _) => n == 2; // the handler's own read finds nothing

        using var response = await host.CompensateAsync(Token(), contentId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var record = Assert.Single(host.Records().Skip(before));
        Assert.Equal(nameof(CompensateTrustedRepositoryObjectCommand), record.RequestType);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal("404", record.Metadata["ResponseStatusCode"]?.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Case4_a_client_abort_inside_the_mark_still_marks_and_deletes_with_None_and_the_test_writer_holds_one_record(bool writerObservesToken)
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());
        var before = host.Audit.Writes.Count;
        host.Storage.DeleteObservesToken = true;
        host.Audit.ObservesToken = writerObservesToken;
        host.Hooks.BeforeMark = context => context?.Abort();

        await CallIgnoringAbortAsync(() => host.CompensateAsync(Token(), contentId, Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        Assert.False(Assert.Single(host.Hooks.MarkTokens).CanBeCanceled);
        Assert.False(Assert.Single(host.Storage.DeleteTokens).CanBeCanceled);
        Assert.True(host.Storage.DeleteResults.Single());
        Assert.Empty(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
        Assert.Equal(0, host.Logs.Count(LogLevel.Warning, "orphan-cleanup"));
        var record = Assert.Single(host.Records().Skip(before));
        Assert.Equal(AuditOperation.Delete, record.Operation);
        Assert.Equal(
            writerObservesToken ? TrustedDocumentRepositoryRefusalAudit.ClientAbortedRequestType : nameof(CompensateTrustedRepositoryObjectCommand),
            record.RequestType);

        host.Hooks.BeforeMark = null;
        host.Audit.ObservesToken = false;
        using var again = await host.CompensateAsync(Token(), contentId);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal("True", Compensations(host).Last().Metadata["alreadyCompensated"]?.ToString());
        Assert.Single(host.Storage.DeleteTokens);
    }

    [Fact]
    public async Task Case4_an_abort_before_the_decision_marks_nothing_deletes_nothing_and_writes_one_client_aborted_record()
    {
        await using var host = await StartAsync();
        var contentId = await host.UploadOkAsync(Token());
        var before = host.Audit.Writes.Count;
        host.Hooks.BeforeIncludingCompensatedRead = (n, context) =>
        {
            if (n == 2) { context?.Abort(); } // the handler's read — the decision has not been made
            return false;
        };

        await CallIgnoringAbortAsync(() => host.CompensateAsync(Token(), contentId, Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        Assert.Empty(host.Hooks.MarkTokens);
        Assert.Empty(host.Storage.DeleteTokens);
        var record = Assert.Single(host.Records().Skip(before));
        Assert.Equal(TrustedDocumentRepositoryRefusalAudit.ClientAbortedRequestType, record.RequestType);
        Assert.False(host.Options.Store.Rows.Single().IsDeleted);
    }

    [Fact]
    public async Task Case4_user_path_twin_the_mark_gets_the_request_token_not_None()
    {
        await using var host = await StartAsync();
        var contentId = await UploadOnUserPathAsync(host);

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/document-repository/objects/{contentId:D}/compensate", UserToken());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(Assert.Single(host.Hooks.MarkTokens).CanBeCanceled);
        Assert.True(Assert.Single(host.Storage.DeleteTokens).CanBeCanceled);
    }

    // ── AC-35 (g) — the bytes landed, the client goes away during the metadata write.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task G_an_abort_during_the_metadata_write_keeps_row_and_binary_and_the_test_writer_holds_one_record(bool writerObservesToken)
    {
        await using var host = await StartAsync();
        host.Audit.ObservesToken = writerObservesToken;
        host.Hooks.BeforeCreate = context => context?.Abort();

        await CallIgnoringAbortAsync(() => host.UploadAsync(Token(), Form.Valid(Encoding.UTF8.GetBytes("landed bytes")), Signal));
        Assert.True(await host.Completed.WaitAsync(TimeSpan.FromSeconds(15)));

        Assert.False(Assert.Single(host.Hooks.CreateTokens).CanBeCanceled);
        var row = Assert.Single(host.Options.Store.Rows);
        Assert.Empty(host.Storage.DeleteTokens);
        Assert.Single(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
        var record = Assert.Single(host.Records());
        Assert.Equal(
            writerObservesToken ? TrustedDocumentRepositoryRefusalAudit.ClientAbortedRequestType : nameof(StoreTrustedRepositoryObjectCommand),
            record.RequestType);
        Assert.Equal(AuditOperation.Create, record.Operation);

        host.Hooks.BeforeCreate = null;
        host.Audit.ObservesToken = false;
        using (var meta = await host.GetAsync(Token(), row.ContentId)) { Assert.Equal(HttpStatusCode.OK, meta.StatusCode); }
        using var content = await host.ContentAsync(Token(), row.ContentId);
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        var bytes = await content.Content.ReadAsByteArrayAsync();
        Assert.Equal(row.Checksum, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public async Task G_user_path_twin_the_metadata_write_gets_the_request_token_not_None()
    {
        await using var host = await StartAsync();

        await UploadOnUserPathAsync(host);

        Assert.True(Assert.Single(host.Hooks.CreateTokens).CanBeCanceled);
    }

    private static void Signal(HttpRequestMessage request) =>
        request.Headers.TryAddWithoutValidation(TestBodyFaults.SignalHeader, "1");

    private static async Task<Guid> UploadOnUserPathAsync(Host host)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/document-repository/objects", UserToken(),
            content: Form.Valid(Encoding.UTF8.GetBytes("user path body"), "user.pdf"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Host.ContentIdAsync(response);
    }

    private static async Task CallIgnoringAbortAsync(Func<Task<HttpResponseMessage>> call)
    {
        try
        {
            using var response = await call();
        }
        catch (Exception)
        {
            // TestServer surfaces the server-side Abort() to the client as an exception.
        }
    }
}
