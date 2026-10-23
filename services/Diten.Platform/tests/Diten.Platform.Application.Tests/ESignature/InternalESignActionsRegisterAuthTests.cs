using System.Net;
using System.Net.Http.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Tests.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// WP-MVP1-SEC-ESIGN1 item 5 — <c>POST api/internal/esign/actions/register</c> (ESIGN-BUNDLE §4.23, CT-D12) is [AllowAnonymous] to the
/// framework and gated by <c>X-Internal-Api-Key</c> (fixed-time compare with AuthService:InternalApiKey, fail closed): no key, a wrong
/// key or a tenant token → 401 and nothing reaches the catalog; the right key passes the gate. This is the proof the internal
/// allow-list entry in LoginOnlyEndpointGuardTests names.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class InternalESignActionsRegisterAuthTests : IClassFixture<ESignatureTestHost>
{
    private static readonly string[] AllKeys = ESignPermissions.All.ToArray();

    private readonly ESignatureTestHost _host;

    public InternalESignActionsRegisterAuthTests(ESignatureTestHost host) => _host = host;

    private static string Token(Guid tenant, Guid user, params string[] keys) => ESignatureTestHost.Token(tenant, user, keys);

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key-of-another-service-0123456789")]
    public async Task Register_without_the_internal_key_is_401_and_registers_nothing(string? apiKey)
    {
        var module = "sec-esign1-" + Guid.NewGuid().ToString("N")[..8];
        using var client = _host.Client();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/internal/esign/actions/register")
        {
            Content = JsonContent.Create(new
            {
                module,
                actions = new[] { new { actionCode = module + ".doc.approve", names = new { tr = "Onay", en = "Approve" }, defaultRequired = true, defaultMeaningCode = "APPROVE", defaultVisualMode = "optional" } }
            })
        };
        if (apiKey is not null) request.Headers.Add("X-Internal-Api-Key", apiKey);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // A tenant token is not an internal key either.
        using var asTenant = _host.Client(Token(Guid.NewGuid(), Guid.NewGuid(), AllKeys));
        Assert.Equal(HttpStatusCode.Unauthorized, (await asTenant.PostAsJsonAsync("api/internal/esign/actions/register", new { module, actions = Array.Empty<object>() })).StatusCode);

        // nothing reached the catalog
        using var reader = _host.Client(Token(Guid.NewGuid(), Guid.NewGuid(), AllKeys));
        var catalog = await reader.GetStringAsync("api/v1/esign/actions");
        Assert.DoesNotContain(module, catalog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_with_the_internal_key_reaches_the_handler()
    {
        using var client = _host.Client();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/internal/esign/actions/register") { Content = JsonContent.Create(new { module = "", actions = Array.Empty<object>() }) };
        request.Headers.Add("X-Internal-Api-Key", ESignatureTestHost.InternalKey);
        var response = await client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode); // past the gate: the handler judges the (empty) body
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
