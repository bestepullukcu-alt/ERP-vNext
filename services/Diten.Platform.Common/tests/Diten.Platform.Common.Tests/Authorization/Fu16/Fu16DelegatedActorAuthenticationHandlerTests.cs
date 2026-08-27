using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Platform.Common.Tests.Authorization.Fu16;

public sealed class Fu16DelegatedActorAuthenticationHandlerTests
{
    [Fact]
    public async Task Exact_proof_publishes_immutable_context_after_authority_allow()
    {
        await using var host = await Fu16TestHost.StartAsync(Fu16AuthorityDisposition.Allowed);
        var response = await host.SendAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("diten.ppm", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, host.Authority.Calls);
    }

    [Theory]
    [InlineData(Fu16AuthorityDisposition.Denied, 403)]
    [InlineData(Fu16AuthorityDisposition.Conflict, 409)]
    [InlineData(Fu16AuthorityDisposition.Unavailable, 503)]
    public async Task Authority_dispositions_are_typed_and_have_zero_endpoint_effect(
        Fu16AuthorityDisposition disposition, int status)
    {
        await using var host = await Fu16TestHost.StartAsync(disposition);
        var response = await host.SendAsync();
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(0, host.EndpointCalls);
    }

    [Fact]
    public async Task Changed_body_is_401_before_authority_or_endpoint()
    {
        await using var host = await Fu16TestHost.StartAsync(Fu16AuthorityDisposition.Allowed);
        var response = await host.SendAsync("changed");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("fu16_request_binding_mismatch", response.Headers.GetValues("X-Diten-Failure-Code").Single());
        Assert.Equal(0, host.Authority.Calls);
        Assert.Equal(0, host.EndpointCalls);
    }

    [Fact]
    public async Task Unknown_key_and_2048_bit_key_fail_before_authority()
    {
        await using var unknown = await Fu16TestHost.StartAsync(Fu16AuthorityDisposition.Allowed, unknownKey: true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unknown.SendAsync()).StatusCode);
        Assert.Equal(0, unknown.Authority.Calls);

        await using var small = await Fu16TestHost.StartAsync(Fu16AuthorityDisposition.Allowed, rsaBits: 2048);
        Assert.Equal(HttpStatusCode.Unauthorized, (await small.SendAsync()).StatusCode);
        Assert.Equal(0, small.Authority.Calls);
    }

    [Fact]
    public async Task Wrong_typ_and_duplicate_header_fail_before_authority()
    {
        await using var wrongType = await Fu16TestHost.StartAsync(Fu16AuthorityDisposition.Allowed, tokenType: "JWT");
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongType.SendAsync()).StatusCode);
        Assert.Equal(0, wrongType.Authority.Calls);

        await using var duplicate = await Fu16TestHost.StartAsync(Fu16AuthorityDisposition.Allowed);
        var duplicateJson = "{\"alg\":\"RS256\",\"typ\":\"diten-delegated-actor-proof+jwt\",\"kid\":\"test-fu16-key\",\"KID\":\"duplicate\"}";
        var token = duplicate.ReplaceHeader(duplicateJson);
        var response = await duplicate.SendTokenAsync(token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, duplicate.Authority.Calls);
    }
}

internal sealed class Fu16TestHost : IAsyncDisposable
{
    private const string Issuer = "https://auth.local";
    private const string Audience = "diten-ppm-service";
    private const string Kid = "test-fu16-key";
    private const string Body = "{\"investmentCaseId\":\"00112233-4455-6677-8899-aabbccddeeff\"}";
    private static readonly Guid Tenant = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private readonly TestServer _server;
    private readonly RSA _rsa;
    private readonly string _token;

    private Fu16TestHost(TestServer server, RSA rsa, string token, RecordingAuthority authority)
    { _server = server; _rsa = rsa; _token = token; Authority = authority; }

    public RecordingAuthority Authority { get; }
    public int EndpointCalls { get; private set; }

    public static async Task<Fu16TestHost> StartAsync(
        Fu16AuthorityDisposition disposition, bool unknownKey = false, int rsaBits = 3072,
        string tokenType = "diten-delegated-actor-proof+jwt")
    {
        var rsa = RSA.Create(rsaBits);
        var key = new RsaSecurityKey(rsa) { KeyId = Kid };
        var authority = new RecordingAuthority(disposition);
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IFu16ValidationKeyProvider>(new FixedKeyProvider(
                    unknownKey ? new(Fu16ValidationKeyDisposition.Unknown, null) : new(Fu16ValidationKeyDisposition.Found, key)));
                services.AddSingleton<IFu16AuthorityDecisionClient>(authority);
                services.AddAuthentication("Existing.HS256")
                    .AddFu16DelegatedActor(options =>
                    {
                        options.TrustedIssuers.Add(Issuer);
                        options.TrustedAudiences.Add(Audience);
                    });
            })
            .Configure(app => app.Run(async context =>
            {
                var result = await context.AuthenticateAsync(Fu16AuthenticationOptions.Scheme);
                if (!result.Succeeded)
                {
                    await context.ChallengeAsync(Fu16AuthenticationOptions.Scheme);
                    return;
                }
                var accessor = context.RequestServices.GetRequiredService<IFu16TrustedRequestContextAccessor>();
                var trusted = Assert.IsType<Fu16TrustedRequestContext>(accessor.Current);
                await context.Response.WriteAsync(trusted.ClientId);
            }));
        var server = new TestServer(builder);
        var requestHash = await HashAsync(Body);
        var token = Issue(key, requestHash, tokenType);
        return new Fu16TestHost(server, rsa, token, authority);
    }

    public async Task<HttpResponseMessage> SendAsync(string body = Body)
        => await SendTokenAsync(_token, body);

    public async Task<HttpResponseMessage> SendTokenAsync(string token, string body = Body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/gate-i")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _server.CreateClient().SendAsync(request);
        if (response.StatusCode == HttpStatusCode.OK) EndpointCalls++;
        return response;
    }

    public string ReplaceHeader(string json)
    {
        var parts = _token.Split('.');
        parts[0] = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        return string.Join('.', parts);
    }

    public ValueTask DisposeAsync()
    {
        _server.Dispose();
        _rsa.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task<string> HashAsync(string body)
    {
        var request = Fu16CanonicalRequestBindingTests.Request("POST", "/internal/gate-i", body);
        return await Fu16CanonicalRequestBinding.ComputeAsync(request, Tenant, "ppm.projects.read", ["ppm.projects.read"]);
    }

    private static string Issue(SecurityKey key, string requestHash, string tokenType)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new[]
        {
            C("tenant_id", Tenant), C("effective_actor_id", Guid.Parse("10112233-4455-6677-8899-aabbccddeeff")),
            C("delegated_actor_id", Guid.Parse("20112233-4455-6677-8899-aabbccddeeff")),
            C("delegation_id", Guid.Parse("30112233-4455-6677-8899-aabbccddeeff")),
            C("service_principal_id", Guid.Parse("40112233-4455-6677-8899-aabbccddeeff")),
            C("credential_id", Guid.Parse("50112233-4455-6677-8899-aabbccddeeff")),
            new Claim("client_id", "diten.ppm"), new Claim("protocol_scope", "diten.s2s.delegated.invoke"),
            new Claim("operation_id", "ppm.projects.read"), new Claim("permission", "ppm.projects.read"),
            new Claim("request_hash", requestHash), new Claim("tenant_grant_version", "4"),
            new Claim("service_principal_version", "5"), new Claim("credential_generation", "6"),
            new Claim(JwtRegisteredClaimNames.Jti, "jti-1"), new Claim("nonce", "nonce-1"),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(Issuer, Audience, claims, now.UtcDateTime, now.AddMinutes(5).UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = tokenType;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Claim C(string type, Guid value) => new(type, value.ToString("D"));

    internal sealed class RecordingAuthority(Fu16AuthorityDisposition disposition) : IFu16AuthorityDecisionClient
    {
        public int Calls { get; private set; }
        public Task<Fu16AuthorityDecision> AuthorizeAndConsumeReplayAsync(Fu16AuthorityRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new Fu16AuthorityDecision(disposition, "decision_" + disposition.ToString().ToLowerInvariant(),
                request.TenantGrantVersion, request.ServicePrincipalVersion, request.CredentialGeneration));
        }
    }

    private sealed class FixedKeyProvider(Fu16ValidationKeyResult result) : IFu16ValidationKeyProvider
    {
        public Task<Fu16ValidationKeyResult> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken) =>
            Task.FromResult(string.Equals(kid, Kid, StringComparison.Ordinal)
                ? result : new Fu16ValidationKeyResult(Fu16ValidationKeyDisposition.Unknown, null));
    }
}
