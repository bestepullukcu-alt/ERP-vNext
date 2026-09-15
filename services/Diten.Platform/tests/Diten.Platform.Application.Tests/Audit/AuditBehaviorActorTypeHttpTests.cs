using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// BL-409 — ONE HTTP ROUND TRIP: a signed tenant token → JWT bearer validation with the Platform's own settings →
/// the real <c>TenantResolutionMiddleware</c> → the production MediatR pipeline → the production audit service →
/// what <c>audit_events</c> would hold.
///
/// <para><b>Why a probe endpoint and not a feature controller.</b> No existing HTTP harness in this project runs the
/// real MediatR pipeline: <c>MeetingHttpTests</c> and <c>RelatedRecordsHttpTests</c> route through a hand-written
/// mediator that calls handlers directly, so <c>AuditBehavior</c> is never on their path. The subject here is the
/// pipeline, so the endpoint does nothing but send one audited command.</para>
///
/// <para><b>The token.</b> Minted here with exactly the claim set AuthService's <c>TokenService.GenerateAccessToken</c>
/// writes for a tenant user (sub, email, given_name, family_name, actor_type, tenant_id, pwd_change_required),
/// HMAC-SHA256 signed and validated with <c>MapInboundClaims = false</c> as <c>AddInfrastructure</c> configures it.
/// The Platform test project does not reference AuthService, so the minting code is not shared.</para>
/// </summary>
public sealed class AuditBehaviorActorTypeHttpTests
{
    private static readonly Guid Tenant = Guid.Parse("40940940-0000-4000-8000-0000000000a1");
    private static readonly Guid User = Guid.Parse("40940940-0000-4000-8000-0000000000a2");

    [Fact]
    public async Task A_real_tenant_token_records_the_command_as_TenantUser_under_its_own_tenant()
    {
        using var host = new Host();

        var response = await host.PostAsync(Host.TenantToken(Tenant, User, "tenant_user"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, host.Calls.Count);

        var write = Assert.Single(host.Outbox.Writes);
        Assert.Equal(Tenant, write.TenantId);
        Assert.Equal(nameof(AuditActorType.TenantUser), write.Payload["ActorType"]);

        var stored = InMemoryAuditOutbox.ToAuditEvent(write);
        Assert.Equal(AuditActorType.TenantUser, stored.ActorType);
        Assert.Equal(User, stored.ActorId);
        Assert.Equal(Tenant, stored.TenantId);
    }

    [Fact]
    public async Task A_signed_token_with_no_actor_type_still_reaches_the_command_which_succeeds_but_leaves_no_record()
    {
        // The one real Unknown path, measured: TenantResolutionMiddleware 403s a NON-EMPTY actor type other than
        // tenant_user on tenant paths, but lets a MISSING one through. AuthService never mints such a token today.
        using var host = new Host();

        var response = await host.PostAsync(Host.TenantToken(Tenant, User, actorType: null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, host.Calls.Count);
        Assert.Empty(host.Outbox.Writes);
    }

    private sealed class Host : IDisposable
    {
        private const string ProbePath = "/api/bl409/audited-probe";
        private const string Issuer = "diten-auth-bl409-test";
        private const string Audience = "diten-platform-bl409-test";
        private const string Secret = "BL-409 http round trip signing key, test only, 0123456789abcdef";

        private readonly TestServer _server;

        public Host()
        {
            var outbox = Outbox;
            var calls = Calls;

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
                    services.AddHttpContextAccessor();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddSingleton<IAuditOutboxWriter>(outbox);

                    services.AddSingleton(calls);
                    services.AddTransient<IRequestHandler<AuditActorProbeCommand, Response<NoContent>>, AuditActorProbeHandler>();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints
                        .MapPost(ProbePath, async (
                            [FromServices] IMediator mediator,
                            [FromServices] ITenantContext tenant,
                            CancellationToken ct) =>
                        {
                            var result = await mediator.Send(new AuditActorProbeCommand(tenant.TenantId), ct);
                            return result.IsSuccessful ? Results.NoContent() : Results.StatusCode(result.StatusCode);
                        })
                        .RequireAuthorization());
                });

            _server = new TestServer(builder);
        }

        public InMemoryAuditOutbox Outbox { get; } = new();

        public AuditActorProbeCalls Calls { get; } = new();

        public async Task<HttpResponseMessage> PostAsync(string bearerToken)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return await client.PostAsync(ProbePath, content: null);
        }

        public static string TenantToken(Guid tenant, Guid user, string? actorType)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, "ayse.yilmaz@tenant.example"),
                new(JwtRegisteredClaimNames.GivenName, "Ayse"),
                new(JwtRegisteredClaimNames.FamilyName, "Yilmaz"),
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
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                    SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public void Dispose() => _server.Dispose();
    }
}
