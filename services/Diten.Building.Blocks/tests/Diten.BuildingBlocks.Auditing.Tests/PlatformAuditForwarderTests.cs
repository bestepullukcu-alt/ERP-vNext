using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.BuildingBlocks.Auditing.Tests;

/// <summary>AUD-001 K4 + K8 — the HTTP body Platform's InternalAuditController reads, and "not silent when unconfigured".</summary>
public sealed class PlatformAuditForwarderTests
{
    private const string Key = "test-internal-key-not-a-real-one";
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Actor = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid Correlation = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private readonly AuditForwardingCounters _counters = new();
    private readonly ListLogger<PlatformAuditForwarder> _logger = new();

    private static PlatformAuditForwarderOptions Configured() => new()
    {
        BaseUrl = "http://platform.test:5057/",
        InternalApiKey = Key,
        SourceService = "Diten.CrmService"
    };

    private static AuditCaller UserCaller(Guid? tenant) =>
        new(tenant, AuditActorType.TenantUser, Actor, "rep@example.test", "Rep Name", Correlation);

    private PlatformAuditForwarder Forwarder(HttpMessageHandler handler, PlatformAuditForwarderOptions options, Guid? tenant = null)
        => new(new SingleClientFactory(handler), Options.Create(options), new FixedCallContext(UserCaller(tenant ?? Tenant)), _counters, _logger);

    private static AuditForwardRequest Request() => new(
        RequestType: "IssueVisitQrCommand",
        Category: AuditCategory.Crm,
        Operation: AuditOperation.Create,
        Outcome: AuditOutcome.Succeeded,
        EntityType: "AccountVisitQr",
        EntityId: Guid.Parse("12121212-3434-5656-7878-909090909090"),
        SourceModule: "MOD-0155",
        Metadata: new Dictionary<string, object?> { ["objectVersion"] = 3 });

    [Fact]
    public async Task Posts_one_append_with_the_key_header_and_the_platform_body()
    {
        var handler = new CapturingHandler();

        var result = await Forwarder(handler, Configured()).ForwardAsync(Request());

        Assert.Equal(AuditForwardResult.Sent, result);
        var (message, _) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, message.Method);
        Assert.Equal("http://platform.test:5057/api/internal/audit/append", message.RequestUri!.ToString());
        Assert.Equal(Key, Assert.Single(message.Headers.GetValues("X-Internal-Api-Key")));

        var body = handler.SingleBody();
        Assert.Equal(Correlation, body.GetProperty("correlationId").GetGuid());
        Assert.Equal("IssueVisitQrCommand", body.GetProperty("requestType").GetString());
        Assert.Equal((int)AuditActorType.TenantUser, body.GetProperty("actorType").GetInt32());
        Assert.Equal(Actor, body.GetProperty("actorId").GetGuid());
        Assert.Equal(Tenant, body.GetProperty("targetTenantId").GetGuid());
        Assert.Equal(18, body.GetProperty("category").GetInt32());
        Assert.Equal((int)AuditOperation.Create, body.GetProperty("operation").GetInt32());
        Assert.Equal((int)AuditOutcome.Succeeded, body.GetProperty("outcome").GetInt32());
        Assert.Equal("AccountVisitQr", body.GetProperty("entityType").GetString());
        Assert.Equal("Diten.CrmService", body.GetProperty("sourceService").GetString());
        Assert.Equal("MOD-0155", body.GetProperty("sourceModule").GetString());
        Assert.False(body.GetProperty("isPlatformGlobal").GetBoolean());
        Assert.Equal(3, body.GetProperty("metadata").GetProperty("objectVersion").GetInt32());
        Assert.Equal(JsonValueKind.String, body.GetProperty("occurredAtUtc").ValueKind);
        Assert.Equal(1, _counters.Sent);
    }

    [Fact]
    public async Task The_body_never_carries_the_internal_key()
    {
        var handler = new CapturingHandler();

        await Forwarder(handler, Configured()).ForwardAsync(Request());

        Assert.DoesNotContain(Key, Assert.Single(handler.Calls).Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, Key, "Diten.CrmService", "BaseUrl")]
    [InlineData("http://platform.test:5057", "", "Diten.CrmService", "InternalApiKey")]
    [InlineData("http://platform.test:5057", Key, " ", "SourceService")]
    public async Task Unconfigured_is_not_silent_a_warning_and_a_skipped_count_and_no_call(
        string? baseUrl, string key, string source, string missing)
    {
        var handler = new CapturingHandler();
        var options = new PlatformAuditForwarderOptions { BaseUrl = baseUrl, InternalApiKey = key, SourceService = source };

        var result = await Forwarder(handler, options).ForwardAsync(Request());

        Assert.Equal(AuditForwardResult.Skipped, result);
        Assert.Empty(handler.Calls);
        Assert.Equal(1, _counters.Skipped);
        var warning = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("NOT written", warning.Message, StringComparison.Ordinal);
        Assert.Contains("IssueVisitQrCommand", warning.Message, StringComparison.Ordinal);
        Assert.Contains(missing, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Message.Contains(Key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_call_without_a_tenant_is_skipped_warned_and_counted()
    {
        var handler = new CapturingHandler();
        var forwarder = new PlatformAuditForwarder(new SingleClientFactory(handler), Options.Create(Configured()),
            new FixedCallContext(UserCaller(null)), _counters, _logger);

        var result = await forwarder.ForwardAsync(Request());

        Assert.Equal(AuditForwardResult.Skipped, result);
        Assert.Empty(handler.Calls);
        Assert.Equal(1, _counters.Skipped);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("no tenant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task A_refusal_from_platform_is_a_warning_and_a_failed_count(HttpStatusCode status)
    {
        var result = await Forwarder(new CapturingHandler(status), Configured()).ForwardAsync(Request());

        Assert.Equal(AuditForwardResult.Failed, result);
        Assert.Equal(1, _counters.Failed);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains(((int)status).ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unreachable_platform_never_throws_into_the_command()
    {
        var result = await Forwarder(new ThrowingHandler(), Configured()).ForwardAsync(Request());

        Assert.Equal(AuditForwardResult.Failed, result);
        Assert.Equal(1, _counters.Failed);
    }

    [Fact]
    public async Task The_startup_check_warns_once_naming_what_is_missing_when_unconfigured()
    {
        var logger = new ListLogger<AuditForwardingStartupCheck>();
        var check = new AuditForwardingStartupCheck(Options.Create(new PlatformAuditForwarderOptions { SourceService = "Diten.CrmService" }), logger);

        await check.StartAsync(default);

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("BaseUrl", warning.Message, StringComparison.Ordinal);
        Assert.Contains("InternalApiKey", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_startup_check_says_on_without_the_key_when_configured()
    {
        var logger = new ListLogger<AuditForwardingStartupCheck>();

        await new AuditForwardingStartupCheck(Options.Create(Configured()), logger).StartAsync(default);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.DoesNotContain(Key, entry.Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }
}
