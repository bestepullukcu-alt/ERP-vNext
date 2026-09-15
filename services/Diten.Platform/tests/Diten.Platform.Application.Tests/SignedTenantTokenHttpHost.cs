using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Platform.Application.Tests;

/// <summary>
/// BL-421 / BL-422 — ONE HTTP ROUND TRIP THROUGH A PRODUCTION PLATFORM CONTROLLER: a signed tenant token → JWT bearer
/// validation with the Platform's settings (<c>MapInboundClaims = false</c>, as <c>AddInfrastructure</c> configures it)
/// → the real <c>TenantResolutionMiddleware</c> → the production <c>[HasPermission]</c> filter → the real controller →
/// the production MediatR pipeline and audit service (<c>AddApplication</c>) → an in-memory audit outbox.
///
/// <para>The token carries the claim set AuthService's <c>TokenService.GenerateAccessToken</c> writes for a tenant user
/// (same as <c>AuditBehaviorActorTypeHttpTests</c>), plus the <c>permission</c> claims <c>[HasPermission]</c> reads. The
/// difference from that host is the endpoint: here it is the shipped controller, not a probe, so what is under test
/// is what a caller over the Gateway actually reaches. A test adds the repositories and clock its endpoint needs.</para>
/// </summary>
internal sealed class SignedTenantTokenHttpHost : IDisposable
{
    public const string CorrelationId = "signed-tenant-token-host-corr";

    private const string Issuer = "diten-auth-signed-host-test";
    private const string Audience = "diten-platform-signed-host-test";
    private const string Secret = "signed tenant token http host, test only, 0123456789abcdef";

    private readonly TestServer _server;

    public SignedTenantTokenHttpHost(Action<IServiceCollection>? configureServices = null)
    {
        var outbox = Outbox;

        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();

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
                services.AddScoped<ICorrelationContext>(_ =>
                {
                    var correlation = new CorrelationContext();
                    correlation.SetCorrelationId(CorrelationId);
                    return correlation;
                });

                services.AddControllers().AddApplicationPart(typeof(PlatformAuditAppendController).Assembly);

                configureServices?.Invoke(services);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseTenantResolution();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
    }

    /// <summary>Every audit write the request produced, as the outbox received it.</summary>
    public InMemoryAuditOutbox Outbox { get; } = new();

    /// <summary>POSTs the JSON exactly as written, so a test states what the wire carries.</summary>
    public async Task<HttpResponseMessage> PostJsonAsync(string path, string bearerToken, string json)
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return await client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    /// <summary>A tenant-user token as AuthService mints it, holding exactly the permissions named.</summary>
    public static string TenantUserToken(Guid tenant, Guid user, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.ToString()),
            new(JwtRegisteredClaimNames.Email, "signed.host@tenant.example"),
            new(JwtRegisteredClaimNames.GivenName, "Signed"),
            new(JwtRegisteredClaimNames.FamilyName, "Host"),
            new("actor_type", "tenant_user"),
            new("tenant_id", tenant.ToString()),
            new("pwd_change_required", "false")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

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
