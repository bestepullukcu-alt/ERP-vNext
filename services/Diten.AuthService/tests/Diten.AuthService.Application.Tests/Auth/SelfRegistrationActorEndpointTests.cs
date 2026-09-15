using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-412 (CT benchmark D6) — both anonymous register routes over real HTTP through the real Api (tenant resolution,
/// MediatR pipeline, Mongo repositories, the production DataSeeder at host start) against a test-owned mongod
/// (<see cref="AccountKindAcceptance.AuthTestHost"/>). Each registration goes into a FRESH disposable tenant, so the
/// handler's own ensure-default-roles fallback provisions Viewer exactly as it would for a real new tenant.
///
/// <para>One outbound edge is replaced, not the client: the registration reads the tenant's login settings from
/// Platform over HTTP. The PRODUCTION <see cref="PlatformTenantLoginSettingsClient"/> still runs (headers, envelope
/// parsing); only its primary HTTP handler answers in-process, so nothing leaves the test process for a shared
/// service. The stub records every path it was asked for, and the tests assert the call happened.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class SelfRegistrationActorEndpointTests : IClassFixture<SelfRegistrationActorEndpointTests.SelfRegistrationTestHost>
{
    private const string SelfRegisteredEvent = "tenant_user_self_registered";

    private readonly SelfRegistrationTestHost _host;

    public SelfRegistrationActorEndpointTests(SelfRegistrationTestHost host) => _host = host;

    [Theory]
    [InlineData("api/auth/register")]
    [InlineData("api/tenant-auth/register")]
    public async Task Self_registration_over_http_stamps_the_role_row_system_and_names_the_new_user_on_the_audit_event(string route)
    {
        var tenantId = Guid.NewGuid();
        var email = $"self-register.{Guid.NewGuid():N}@acceptance.invalid";
        using var client = _host.Client(tenantHeader: tenantId);

        var response = await client.PostAsJsonAsync(route, new
        {
            email,
            password = AccountKindAcceptance.DisposablePassword,
            firstName = "Self",
            lastName = "Registrant"
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{route}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var database = sp.GetRequiredService<IMongoDatabase>();

        var user = await sp.GetRequiredService<IUserRepository>().GetByEmailAndTenantAsync(email, tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException($"{route} answered 201 but the user was not persisted.");
        var viewer = await sp.GetRequiredService<IRoleRepository>().GetByNameAndTenantAsync("Viewer", tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException($"{route} answered 201 but the tenant has no Viewer role.");

        // The default-role row: tenant policy's act, the non-person actor.
        var row = Assert.Single(await database.GetCollection<UserRole>("userRoles")
            .Find(ur => ur.UserId == user.Id && ur.TenantId == tenantId)
            .ToListAsync());
        Assert.Equal(viewer.Id, row.RoleId);
        Assert.Equal("system", row.AssignedBy);
        Assert.Equal("system", row.CreatedBy);

        // The attributable act: exactly one self-registration event for this tenant, and its actor is the new user.
        var audit = Assert.Single(await database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(a => a.EventName == SelfRegisteredEvent && a.TenantId == tenantId)
            .ToListAsync());
        Assert.Equal(user.Id, audit.UserId);

        using var metadata = JsonDocument.Parse(audit.Metadata);
        var root = metadata.RootElement;
        Assert.Equal(user.Id.ToString(), root.GetProperty("actorId").GetString());
        Assert.Equal("Viewer", root.GetProperty("defaultRole").GetString());
        Assert.Equal(viewer.Id.ToString(), root.GetProperty("defaultRoleId").GetString());
        Assert.Equal("system", root.GetProperty("roleAssignedBy").GetString());
        Assert.Contains("default role Viewer assigned by tenant policy", root.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(email, audit.Metadata, StringComparison.OrdinalIgnoreCase);

        // The production login-settings client really ran for this tenant, against the in-process edge only.
        Assert.Contains($"/api/internal/tenants/{tenantId:D}/login-settings", _host.PlatformRequests);
    }

    /// <summary>The shared acceptance host, with the Platform login-settings network edge answered in-process.</summary>
    public sealed class SelfRegistrationTestHost : AccountKindAcceptance.AuthTestHost
    {
        public ConcurrentQueue<string> PlatformRequests { get; } = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            // Same typed client registration as Infrastructure's AddHttpClient<ITenantLoginSettingsClient,
            // PlatformTenantLoginSettingsClient>: the BaseAddress/timeout configuration stays, only the primary handler
            // changes. A new handler instance per build, so IHttpClientFactory's handler rotation never disposes a shared one.
            services.AddHttpClient<ITenantLoginSettingsClient, PlatformTenantLoginSettingsClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new PlatformLoginSettingsEdge(PlatformRequests));
        }
    }

    private sealed class PlatformLoginSettingsEdge(ConcurrentQueue<string> requests) : HttpMessageHandler
    {
        private static readonly Regex LoginSettingsPath = new(
            @"^/api/internal/tenants/(?<tenant>[0-9a-fA-F-]{36})/login-settings$", RegexOptions.Compiled);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            requests.Enqueue(path);

            var match = LoginSettingsPath.Match(path);
            if (request.Method != HttpMethod.Get || !match.Success)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var snapshot = new TenantLoginSettingsSnapshot(
                Guid.Parse(match.Groups["tenant"].Value),
                TwoFactorEnabled: false,
                MfaRequired: false,
                EmailLoginEnabled: true,
                PhoneLoginEnabled: false,
                PasswordMinLength: 8,
                PasswordRequireUppercase: true,
                PasswordRequireLowercase: true,
                PasswordRequireDigit: true,
                PasswordRequireSpecialChar: false,
                PasswordExpirationDays: null,
                SessionTimeoutMinutes: 30,
                RefreshTokenLifetimeDays: 7,
                MaxFailedLoginAttempts: 5,
                LockoutDurationMinutes: 15);
            var body = JsonSerializer.Serialize(new { succeeded = true, message = (string?)null, data = snapshot });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
