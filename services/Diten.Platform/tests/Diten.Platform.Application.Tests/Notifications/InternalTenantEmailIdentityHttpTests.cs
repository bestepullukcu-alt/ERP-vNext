using System.Net;
using System.Text.Json;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Doubles = Diten.Platform.Application.Tests.Notifications.NotificationsSmtpIntegrationTests;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — <c>GET /api/internal/tenants/{id}/email-identity</c> over real HTTP: routing, the API-key gate, the JSON
/// that goes on the wire. Real: the controller, MVC's pipeline and serializer, the query handler and
/// <see cref="TenantEmailIdentityResolver"/>. Doubled: the tenant registry and the settings repository (in memory).
/// </summary>
public sealed class InternalTenantEmailIdentityHttpTests
{
    private const string ApiKey = "test-internal-api-key";
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task Without_the_internal_key_the_answer_is_401_and_nothing_is_looked_up()
    {
        using var host = new Host();

        var missing = await host.GetAsync(Tenant, apiKey: null);
        var wrong = await host.GetAsync(Tenant, apiKey: "not-the-key");
        // …and the same for a tenant that does not exist: without the key there is no telling which is which.
        var unknown = await host.GetAsync(Guid.NewGuid(), apiKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(0, host.Mediator.Sent);
    }

    [Fact]
    public async Task A_wrong_key_of_the_same_length_is_refused()
    {
        using var host = new Host();
        var sameLength = new string('x', ApiKey.Length);

        var response = await host.GetAsync(Tenant, sameLength);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Mediator.Sent);
    }

    [Fact]
    public async Task With_the_key_it_returns_the_four_presentation_values_and_nothing_else()
    {
        using var host = new Host();
        host.Settings.CreateAsync(new TenantMessagingSettings
        {
            TenantId = Tenant,
            ProviderCode = MessagingProviderCode.Smtp,
            SenderEmail = "secret-sender@ditenpharma.test",
            SenderName = "Diten Pharma İK",
            ReplyToEmail = "ik@ditenpharma.test",
            Host = "smtp.internal.ditenpharma.test",
            Port = 2525,
            CredentialSecretRef = "secret:tenant:smtp:diten",
            IsEnabled = true
        }).GetAwaiter().GetResult();

        var response = await host.GetAsync(Tenant, ApiKey);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(
            ["displayName", "language", "replyToEmail", "senderName", "tenantId"],
            data.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Diten Pharma", data.GetProperty("displayName").GetString());
        Assert.Equal("tr", data.GetProperty("language").GetString());
        Assert.Equal("Diten Pharma İK", data.GetProperty("senderName").GetString());
        Assert.Equal("ik@ditenpharma.test", data.GetProperty("replyToEmail").GetString());

        // No sender address, no SMTP setting, no credential reference — anywhere in the answer.
        Assert.DoesNotContain("secret-sender", body);
        Assert.DoesNotContain("smtp.internal", body);
        Assert.DoesNotContain("2525", body);
        Assert.DoesNotContain("secret:tenant", body);
    }

    [Fact]
    public async Task A_tenant_without_its_own_settings_has_no_sender_name_of_its_own()
    {
        using var host = new Host();
        host.Settings.CreateAsync(new TenantMessagingSettings
        {
            TenantId = null,
            IsPlatformDefault = true,
            SenderEmail = "bildirim@di10.test",
            SenderName = "Diten PPM",
            IsEnabled = true
        }).GetAwaiter().GetResult();

        var body = await (await host.GetAsync(Tenant, ApiKey)).Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").GetProperty("senderName").ValueKind);
        Assert.DoesNotContain("Diten PPM", body);
    }

    [Fact]
    public async Task A_tenant_that_does_not_exist_gets_an_answer_that_names_nothing()
    {
        using var host = new Host();
        host.Tenants.Tenant = null;
        var asked = Guid.NewGuid();

        var response = await host.GetAsync(asked, ApiKey);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(asked.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;

        public EmailShellDispatchTests.FakeTenants Tenants { get; } = new()
        {
            Tenant = new Tenant
            {
                Code = "DITEN",
                Slug = "diten",
                Name = "diten-pharma",
                DisplayName = "Diten Pharma",
                Domain = "diten.test"
            }
        };

        public Doubles.InMemoryTenantMessagingSettingsRepository Settings { get; } = new();
        public CountingMediator Mediator { get; }

        public Host()
        {
            Mediator = new CountingMediator(new GetTenantEmailIdentityQueryHandler(
                new TenantEmailIdentityResolver(Tenants, Settings, new FakeNotificationLocaleResolver("tr"))));

            _server = new TestServer(new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthService:InternalApiKey"] = ApiKey
                }))
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddSingleton<IMediator>(Mediator);
                    services.AddControllers()
                        .ConfigureApplicationPartManager(manager =>
                        {
                            // Only the controller under test: the API assembly's other controllers need the whole
                            // platform container, and none of them is this test's subject.
                            manager.ApplicationParts.Clear();
                            manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalTenantEmailIdentityController).Assembly));
                            manager.FeatureProviders.Add(new OnlyController(typeof(InternalTenantEmailIdentityController)));
                        });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }));
        }

        public Task<HttpResponseMessage> GetAsync(Guid tenantId, string? apiKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId:D}/email-identity");
            if (apiKey is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", apiKey);
            }

            return _server.CreateClient().SendAsync(request);
        }

        public void Dispose() => _server.Dispose();
    }

    private sealed class OnlyController(Type controller) : ControllerFeatureProvider
    {
        protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
    }

    private sealed class CountingMediator(GetTenantEmailIdentityQueryHandler handler) : IMediator
    {
        public int Sent { get; private set; }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent++;
            return (TResponse)(object?)await handler.Handle((GetTenantEmailIdentityQuery)(object)request, cancellationToken)!;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
