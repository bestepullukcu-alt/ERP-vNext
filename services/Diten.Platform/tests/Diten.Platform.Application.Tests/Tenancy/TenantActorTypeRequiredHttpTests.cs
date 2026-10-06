using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenancy;

/*
 * WP-INFRA-ACTOR-TYPE-REQUIRED-01 (BL-409 remainder) — A SIGNED-IN PRINCIPAL WITH NO ACTOR TYPE IS REFUSED ON A
 * TENANT ROUTE, EXACTLY AS AN UNRECOGNISED ONE IS.
 *
 * WHAT WAS WRONG. BL-409 made audited commands record the token's actor type. A signed-in principal whose token has
 * no actor_type resolves to Unknown, the audit service refuses Unknown, and the command runs with no audit record
 * (a warning only). Diten.Platform.Common's TenantResolutionMiddleware answered an UNRECOGNISED actor_type 403 on
 * tenant routes but let a MISSING or blank one through — the guard was written `!IsNullOrWhiteSpace(actorType) && ...`.
 *
 * THE RULE. On a tenant route (the ordinary tenant branch, the tenant-scoped /api/platform/* groups and tenant-mode
 * personalization), an AUTHENTICATED principal must carry actor_type = tenant_user. Missing and blank get the same
 * status and the same body as an unrecognised value, and no claim value is echoed.
 *
 * ⚠ SCOPED TO AN AUTHENTICATED PRINCIPAL ON PURPOSE. A request with no token, or with a token the bearer handler
 * rejected, has no actor to name; it keeps today's behaviour (header tenant, then [Authorize] answers 401). Bypass
 * paths such as /api/internal (API-key routes) are answered before any actor is read at all.
 *
 * ONE HTTP ROUND TRIP PER CASE: a signed token → JWT bearer validation with the Platform's settings
 * (MapInboundClaims = false, as Infrastructure DependencyInjection.AddInfrastructure configures it) → the REAL
 * middleware via the production UseTenantResolution() → a probe endpoint that only records that it ran. The token
 * carries the claim set AuthService's TokenService.GenerateAccessToken writes for a tenant user, with actor_type
 * varied. The Platform test project does not reference AuthService, so the minting code is not shared.
 *
 * ⚠ NOT A VACUITY CHECK. Every refusal asserts the endpoint DID NOT RUN, the tenant_user control proves a middleware
 * refusing everything fails, and the blank case first proves the blank claim really arrives on the validated
 * principal — otherwise "blank" would silently repeat "missing".
 */
public sealed class TenantActorTypeRequiredHttpTests
{
    private const string TenantUser = "tenant_user";
    private const string UnrecognisedActorType = "wp_unrecognised_actor";
    private const string Email = "actor.type.probe@tenant.example";

    private static readonly Guid Tenant = Guid.Parse("7e4a0409-0000-4000-8000-0000000000b1");
    private static readonly Guid User = Guid.Parse("7e4a0409-0000-4000-8000-0000000000b2");

    public static TheoryData<string> TenantRoutes() => new()
    {
        Host.TenantRoute,
        Host.TenantScopedOrgRoute,
        Host.PersonalizationRoute
    };

    public static TheoryData<string, string> TenantRoutesWithBlankActorTypes()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in new[] { Host.TenantRoute, Host.TenantScopedOrgRoute, Host.PersonalizationRoute })
        {
            data.Add(route, string.Empty);
            data.Add(route, "   ");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TenantRoutes))]
    public async Task A_signed_token_with_no_actor_type_is_refused_on_a_tenant_route(string route)
    {
        using var host = new Host();
        var token = Host.Token(Tenant, User, actorType: null);

        var inbound = await host.ReadInboundActorTypeAsync(token);
        Assert.False(inbound.Present, "the probe token was meant to carry no actor_type claim at all");

        var response = await host.SendAsync(route, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden Actor", (await Problem.ReadAsync(response)).Title);
        Assert.Empty(host.Hits);
    }

    [Theory]
    [MemberData(nameof(TenantRoutesWithBlankActorTypes))]
    public async Task A_signed_token_with_a_blank_actor_type_is_refused_on_a_tenant_route(string route, string blank)
    {
        using var host = new Host();
        var token = Host.Token(Tenant, User, blank);

        // Not a second "missing" case: the blank claim survives bearer validation and is what the middleware reads.
        var inbound = await host.ReadInboundActorTypeAsync(token);
        Assert.True(inbound.Present, "the blank actor_type did not reach the principal, so this case would only repeat the missing one");
        Assert.Equal(blank, inbound.Value);

        var response = await host.SendAsync(route, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden Actor", (await Problem.ReadAsync(response)).Title);
        Assert.Empty(host.Hits);
    }

    [Theory]
    [MemberData(nameof(TenantRoutes))]
    public async Task A_tenant_user_token_passes_and_resolves_its_own_tenant(string route)
    {
        using var host = new Host();

        var response = await host.SendAsync(route, Host.Token(Tenant, User, TenantUser));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hit = Assert.Single(host.Hits);
        Assert.Equal(route, hit.Path);
        Assert.Equal(Tenant, hit.TenantId);
    }

    [Theory]
    [MemberData(nameof(TenantRoutes))]
    public async Task An_unrecognised_actor_type_is_still_refused_unchanged(string route)
    {
        using var host = new Host();

        var response = await host.SendAsync(route, Host.Token(Tenant, User, UnrecognisedActorType));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden Actor", (await Problem.ReadAsync(response)).Title);
        Assert.Empty(host.Hits);
    }

    [Theory]
    [MemberData(nameof(TenantRoutes))]
    public async Task The_missing_and_blank_refusals_are_the_unrecognised_refusal_and_echo_no_claim_value(string route)
    {
        using var host = new Host();

        var missingResponse = await host.SendAsync(route, Host.Token(Tenant, User, actorType: null));
        var blankResponse = await host.SendAsync(route, Host.Token(Tenant, User, string.Empty));
        var unrecognisedResponse = await host.SendAsync(route, Host.Token(Tenant, User, UnrecognisedActorType));

        Assert.Equal(HttpStatusCode.Forbidden, unrecognisedResponse.StatusCode);
        Assert.Equal(unrecognisedResponse.StatusCode, missingResponse.StatusCode);
        Assert.Equal(unrecognisedResponse.StatusCode, blankResponse.StatusCode);

        var unrecognised = await Problem.ReadAsync(unrecognisedResponse);
        foreach (var refusal in new[] { await Problem.ReadAsync(missingResponse), await Problem.ReadAsync(blankResponse) })
        {
            Assert.Equal(unrecognised.ContentType, refusal.ContentType);
            Assert.Equal(unrecognised.Title, refusal.Title);
            Assert.Equal(unrecognised.Status, refusal.Status);
            Assert.Equal(unrecognised.Detail, refusal.Detail);
            Assert.Equal(unrecognised.PropertyNames, refusal.PropertyNames);
        }

        foreach (var raw in new[] { unrecognised.Raw, (await Problem.ReadAsync(missingResponse)).Raw })
        {
            Assert.DoesNotContain(UnrecognisedActorType, raw);
            Assert.DoesNotContain(Tenant.ToString(), raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(User.ToString(), raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Email, raw, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Empty(host.Hits);
    }

    [Fact]
    public async Task An_anonymous_internal_api_key_route_is_unaffected()
    {
        using var host = new Host();

        var withKey = await host.SendAsync(Host.InternalRoute, bearer: null, internalApiKey: Host.InternalApiKey);
        Assert.Equal(HttpStatusCode.OK, withKey.StatusCode);
        Assert.Single(host.Hits);

        // The probe really checks the key, so the 200 above is not an endpoint that accepts anything.
        var wrongKey = await host.SendAsync(Host.InternalRoute, bearer: null, internalApiKey: "not-the-key");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
        Assert.Single(host.Hits);

        // The /api/internal bypass is answered before any actor is read, even if a caller also sent such a token.
        var withKeyAndNoActorToken = await host.SendAsync(
            Host.InternalRoute,
            bearer: Host.Token(Tenant, User, actorType: null),
            internalApiKey: Host.InternalApiKey);
        Assert.Equal(HttpStatusCode.OK, withKeyAndNoActorToken.StatusCode);
        Assert.Equal(2, host.Hits.Count);
    }

    [Fact]
    public async Task A_request_with_no_token_has_no_principal_and_keeps_todays_header_resolution()
    {
        using var host = new Host();

        var response = await host.SendAsync(Host.AnonymousTenantRoute, bearer: null, tenantHeader: Tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hit = Assert.Single(host.Hits);
        Assert.Equal(Tenant, hit.TenantId);
    }

    /// <summary>
    /// A token the bearer handler rejects is not a principal, so the new refusal does not apply to it — and it still
    /// never reaches a protected endpoint. Both answers are today's, measured before this WP changed anything:
    /// with no other tenant signal the middleware answers Missing Tenant; with one, [Authorize] answers 401.
    /// </summary>
    [Fact]
    public async Task A_token_the_bearer_handler_rejects_is_not_a_principal_and_never_reaches_a_protected_endpoint()
    {
        using var host = new Host();
        var forged = Host.Token(Tenant, User, actorType: null, secret: "a different signing key that Platform does not trust, 0123456789");

        var withoutHeader = await host.SendAsync(Host.TenantRoute, forged);
        Assert.Equal(HttpStatusCode.BadRequest, withoutHeader.StatusCode);
        Assert.Equal("Missing Tenant", (await Problem.ReadAsync(withoutHeader)).Title);

        var withHeader = await host.SendAsync(Host.TenantRoute, forged, tenantHeader: Tenant);
        Assert.Equal(HttpStatusCode.Unauthorized, withHeader.StatusCode);

        Assert.Empty(host.Hits);
    }

    private sealed record Hit(string Path, Guid? TenantId);

    private sealed record Problem(
        string? ContentType,
        string Raw,
        string? Title,
        int? Status,
        string? Detail,
        string[] PropertyNames)
    {
        public static async Task<Problem> ReadAsync(HttpResponseMessage response)
        {
            var raw = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrWhiteSpace(raw), $"expected a problem body, got HTTP {(int)response.StatusCode} with an empty body");

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            return new Problem(
                response.Content.Headers.ContentType?.MediaType,
                raw,
                root.TryGetProperty("title", out var title) ? title.GetString() : null,
                root.TryGetProperty("status", out var status) ? status.GetInt32() : null,
                root.TryGetProperty("detail", out var detail) ? detail.GetString() : null,
                root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }
    }

    private sealed class Host : IDisposable
    {
        public const string TenantRoute = "/api/users/wp-actor-type-probe";
        public const string TenantScopedOrgRoute = "/api/platform/organization-units/wp-actor-type-probe";
        public const string PersonalizationRoute = "/api/personalization/wp-actor-type-probe";
        public const string AnonymousTenantRoute = "/api/users/wp-actor-type-anonymous-probe";
        public const string InternalRoute = "/api/internal/wp-actor-type-probe";
        public const string InternalApiKey = "wp-infra-actor-type-required-01 internal key, test only";

        /// <summary>Under the /api/internal bypass: reports what the validated principal carries, never gated.</summary>
        private const string InboundClaimRoute = "/api/internal/wp-actor-type-inbound-claim";

        private const string Issuer = "diten-auth-wp-actor-type-test";
        private const string Audience = "diten-platform-wp-actor-type-test";
        private const string Secret = "WP-INFRA-ACTOR-TYPE-REQUIRED-01 signing key, test only, 0123456789abcdef";

        private readonly TestServer _server;

        public Host()
        {
            var hits = Hits;

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();

                    // The Platform's bearer settings (Infrastructure DependencyInjection.AddInfrastructure).
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = Issuer,
                                ValidAudience = Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                                ClockSkew = TimeSpan.Zero
                            };
                        });
                    services.AddAuthorization();
                    services.AddScoped<ITenantContext, TenantContext>();
                })
                .Configure(app =>
                {
                    // Same order as Diten.Platform.API Program.cs: authentication, tenant resolution, authorization.
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        foreach (var route in new[] { TenantRoute, TenantScopedOrgRoute, PersonalizationRoute })
                        {
                            endpoints.MapGet(route, (HttpContext http, [FromServices] ITenantContext tenant) =>
                                {
                                    hits.Enqueue(new Hit(http.Request.Path.Value!, ResolvedTenant(tenant)));
                                    return Results.Ok();
                                })
                                .RequireAuthorization();
                        }

                        endpoints.MapGet(AnonymousTenantRoute, (HttpContext http, [FromServices] ITenantContext tenant) =>
                            {
                                hits.Enqueue(new Hit(http.Request.Path.Value!, ResolvedTenant(tenant)));
                                return Results.Ok();
                            })
                            .AllowAnonymous();

                        endpoints.MapGet(InternalRoute, (HttpContext http) =>
                            {
                                var provided = http.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
                                if (!string.Equals(provided, InternalApiKey, StringComparison.Ordinal))
                                {
                                    return Results.Unauthorized();
                                }

                                hits.Enqueue(new Hit(http.Request.Path.Value!, null));
                                return Results.Ok();
                            })
                            .AllowAnonymous();

                        endpoints.MapGet(InboundClaimRoute, (HttpContext http) =>
                            {
                                var claim = http.User.FindFirst("actor_type");
                                return Results.Json(new { present = claim is not null, value = claim?.Value });
                            })
                            .RequireAuthorization();
                    });
                });

            _server = new TestServer(builder);
        }

        public ConcurrentQueue<Hit> Hits { get; } = new();

        public async Task<HttpResponseMessage> SendAsync(
            string path,
            string? bearer,
            string? internalApiKey = null,
            Guid? tenantHeader = null)
        {
            var client = _server.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (bearer is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            }

            if (internalApiKey is not null)
            {
                request.Headers.Add("X-Internal-Api-Key", internalApiKey);
            }

            if (tenantHeader is not null)
            {
                request.Headers.Add("X-Tenant-Id", tenantHeader.Value.ToString());
            }

            return await client.SendAsync(request);
        }

        public async Task<(bool Present, string? Value)> ReadInboundActorTypeAsync(string bearer)
        {
            var response = await SendAsync(InboundClaimRoute, bearer);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            var value = root.GetProperty("value");
            return (root.GetProperty("present").GetBoolean(), value.ValueKind == JsonValueKind.Null ? null : value.GetString());
        }

        public static string Token(Guid tenant, Guid user, string? actorType, string secret = Secret)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, Email),
                new(JwtRegisteredClaimNames.GivenName, "Probe"),
                new(JwtRegisteredClaimNames.FamilyName, "Actor"),
                new("tenant_id", tenant.ToString()),
                new("pwd_change_required", "false")
            };

            if (actorType is not null)
            {
                claims.Add(new Claim("actor_type", actorType));
            }

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public void Dispose() => _server.Dispose();

        private static Guid? ResolvedTenant(ITenantContext tenant)
            => tenant is TenantContext { IsResolved: true, IsPlatformContext: false } ? tenant.TenantId : null;
    }
}
