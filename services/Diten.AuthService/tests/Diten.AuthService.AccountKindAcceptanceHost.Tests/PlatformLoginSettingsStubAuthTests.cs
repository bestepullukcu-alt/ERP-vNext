// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F8 (Aşama E): the login-settings stub's X-Internal-Api-Key check (previously
// only a comment claim, no corresponding code) is now real — this proves it directly against the real stub
// (InternalsVisibleTo, no real host OS process needed for this specific check): a missing or wrong key is a real
// 401, the correct key is a real 200. ReadyMessageLabelTests/WireLevelErrorMappingTests already prove, out of
// process, that the real API child DOES present the correct key (P7's pre-ready login only succeeds because of
// it) — this file proves the REJECTION side, which no out-of-process run naturally exercises (the real client
// never sends a wrong key).

using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

public sealed class PlatformLoginSettingsStubAuthTests
{
    private const string HeaderName = "X-Internal-Api-Key";

    [Fact]
    public async Task MissingHeader_Is401()
    {
        var stub = await PlatformLoginSettingsStub.StartAsync("expected-key-abc");
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(stub.BaseUrl) };
            using var resp = await client.GetAsync($"api/internal/tenants/{Guid.NewGuid():D}/login-settings");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
        finally
        {
            stub.Dispose();
        }
    }

    [Fact]
    public async Task WrongHeaderValue_Is401()
    {
        var stub = await PlatformLoginSettingsStub.StartAsync("expected-key-abc");
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(stub.BaseUrl) };
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/internal/tenants/{Guid.NewGuid():D}/login-settings");
            req.Headers.Add(HeaderName, "definitely-not-the-right-key");
            using var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
        finally
        {
            stub.Dispose();
        }
    }

    [Fact]
    public async Task CorrectHeaderValue_Is200()
    {
        var stub = await PlatformLoginSettingsStub.StartAsync("expected-key-abc");
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(stub.BaseUrl) };
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/internal/tenants/{Guid.NewGuid():D}/login-settings");
            req.Headers.Add(HeaderName, "expected-key-abc");
            using var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }
        finally
        {
            stub.Dispose();
        }
    }
}
