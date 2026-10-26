using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.ESignature;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// WP-MVP1-SEC-ESIGN1 item 4 over HTTP (the real Platform host on a test-owned replica set, never 27017):
/// <list type="bullet">
/// <item>§4.17 sample image — <c>[HasAnyPermission(my-samples.manage, samples.approve, signatures.read)]</c>: EACH key alone is
/// enough (200 — the owner, an approver of the pending sample, a reader of a signature that shows it); none of them → 403;
/// anonymous → 401;</item>
/// <item>§4.20 listMeanings — <c>[HasAnyPermission(meanings.read, signatures.sign)]</c>: each key alone 200; none 403; anonymous 401.</item>
/// </list>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class ESignatureAnyPermissionHttpTests : IClassFixture<ESignatureTestHost>
{
    private const string Samples = "api/v1/esign/samples";
    private static readonly string[] AllKeys = ESignPermissions.All.ToArray();
    private const string Unrelated = ESignPermissions.PolicyRead; // an e-sign key that is NOT one of either action's keys

    private readonly ESignatureTestHost _host;

    public ESignatureAnyPermissionHttpTests(ESignatureTestHost host) => _host = host;

    private static string Token(Guid tenant, Guid user, params string[] keys) => ESignatureTestHost.Token(tenant, user, keys);

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private async Task<HttpStatusCode> GetAsync(string? token, string path)
    {
        using var client = _host.Client(token);
        return (await client.GetAsync(path)).StatusCode;
    }

    /// <summary>
    /// Anonymous as the Gateway sends it: no token, the tenant in X-Tenant-Id. (With no tenant at all TenantResolutionMiddleware
    /// answers 400 "Missing Tenant" before any authorization filter runs — its documented order.)
    /// </summary>
    private async Task<HttpStatusCode> AnonymousAsync(string path)
    {
        using var client = _host.Client();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        return (await client.GetAsync(path)).StatusCode;
    }

    /// <summary>A tenant whose owner saved a sample; returns the sample id (Pending under the default approval rule).</summary>
    private async Task<(Guid Tenant, Guid Owner, Guid SampleId)> SampleAsync()
    {
        var tenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        using var client = _host.Client(Token(tenant, owner, AllKeys));
        var response = await client.PostAsJsonAsync(Samples, new
        {
            ticket = _host.Tickets.Issue(tenant, owner, "sample"), kind = "drawn", pngBase64 = Convert.ToBase64String(TestPng.Create(220, 80)),
            strokes = new[] { new[] { new[] { 0.1, 0.2 }, new[] { 0.3, 0.4 } } }, idempotencyKey = Key()
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var doc = JsonDocument.Parse(body);
        return (tenant, owner, doc.RootElement.GetProperty("data").GetProperty("id").GetGuid());
    }

    // ── §4.17 sample image ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Image_the_owner_key_alone_is_enough()
    {
        var (tenant, owner, id) = await SampleAsync();
        Assert.Equal(HttpStatusCode.OK, await GetAsync(Token(tenant, owner, ESignPermissions.MySamplesManage), $"{Samples}/{id}/image"));
    }

    [Fact]
    public async Task Image_the_approver_key_alone_is_enough()
    {
        var (tenant, _, id) = await SampleAsync();
        Assert.Equal(HttpStatusCode.OK, await GetAsync(Token(tenant, Guid.NewGuid(), ESignPermissions.SamplesApprove), $"{Samples}/{id}/image"));
    }

    [Fact]
    public async Task Image_the_signature_read_key_alone_is_enough_for_a_sample_shown_on_a_signature()
    {
        var (tenant, owner, id) = await SampleAsync();
        using (var approver = _host.Client(Token(tenant, Guid.NewGuid(), AllKeys)))
        {
            Assert.Equal(HttpStatusCode.OK, (await approver.PostAsync($"{Samples}/{id}/approve", null)).StatusCode);
        }

        using (var signer = _host.Client(Token(tenant, owner, AllKeys)))
        {
            var signed = await signer.PostAsJsonAsync("api/v1/esign/signatures", new
            {
                ticket = _host.Tickets.Issue(tenant, owner, "sign", ESignatureReauthKind.Password, DateTime.UtcNow.AddMinutes(-1)),
                subject = new { module = TestSubjectProvider.Module, subjectType = TestSubjectProvider.SubjectType, subjectId = _host.Subjects.Add(tenant) },
                meaningCode = "APPROVE", appearance = new { type = "sample", sampleId = id }, idempotencyKey = Key()
            });
            Assert.True(signed.StatusCode == HttpStatusCode.Created, await signed.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.OK, await GetAsync(Token(tenant, Guid.NewGuid(), ESignPermissions.SignaturesRead), $"{Samples}/{id}/image"));
    }

    [Fact]
    public async Task Image_without_any_of_the_three_keys_is_403_and_anonymous_is_401()
    {
        var (tenant, owner, id) = await SampleAsync();
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(Token(tenant, owner, Unrelated), $"{Samples}/{id}/image"));
        Assert.Equal(HttpStatusCode.Unauthorized, await AnonymousAsync($"{Samples}/{id}/image"));
    }

    // ── §4.20 listMeanings ────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ESignPermissions.MeaningsRead)]
    [InlineData(ESignPermissions.SignaturesSign)]
    public async Task ListMeanings_either_key_alone_is_enough(string key)
        => Assert.Equal(HttpStatusCode.OK, await GetAsync(Token(Guid.NewGuid(), Guid.NewGuid(), key), "api/v1/esign/meanings"));

    [Fact]
    public async Task ListMeanings_without_either_key_is_403_and_anonymous_is_401()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(Token(Guid.NewGuid(), Guid.NewGuid(), Unrelated), "api/v1/esign/meanings"));
        Assert.Equal(HttpStatusCode.Unauthorized, await AnonymousAsync("api/v1/esign/meanings"));
    }
}
