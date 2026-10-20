using System.Net;
using System.Text.Json;
using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Application.Tests.Segmentation;
using Diten.CrmService.Application.Tests.VisitWorkspace;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Audit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.CrmService.Application.Tests.Audit;

/// <summary>
/// WP-VW-W6 F8-a (AUD-001 K4, path b) — CRM's REAL pipeline (<c>AddApplication</c>) and CRM's REAL forwarder settings
/// (<c>AddCrmAuditForwarding</c>); only the store and Platform's HTTP answer are fakes. A marked command makes exactly one
/// append call (succeeded or refused); an unmarked command and a query make none.
/// </summary>
public sealed class CrmAuditForwardingPipelineTests
{
    private const string Key = "pipeline-test-internal-key";
    private static readonly Guid Tenant = Guid.Parse("97c5aaaa-0000-4000-8000-000000000001");

    private readonly CapturingPlatform _platform = new();
    private readonly FakeNotificationSettings _settings = new();

    private ServiceProvider Build(string? baseUrl = "http://platform.test:5057", string? key = Key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlatformService:BaseUrl"] = baseUrl,
                ["AuthService:InternalApiKey"] = key
            })
            .Build();

        var tenant = new TenantContext();
        tenant.SetTenant(Tenant);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddCrmAuditForwarding(configuration);
        services.AddHttpClient(PlatformAuditForwarder.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _platform);
        services.AddSingleton<ITenantContext>(tenant);
        services.AddSingleton<IActorContext>(new FixedActorContext("rep-1"));
        services.AddSingleton<IVisitNotificationSettingsRepository>(_settings);
        services.AddSingleton<IVisitEvidenceAuditPublisher>(new RecordingAudit());
        services.AddTransient<IRequestHandler<UnmarkedProbeCommand, Response<int>>, UnmarkedProbeHandler>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task<T> Send<T>(ServiceProvider provider, IRequest<T> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private static UpdateVisitNotificationSettingsCommand Update(int? expectedVersion = null)
        => new(new VisitNotificationSettingsInput("pv.desk@example.test", null, true, null, expectedVersion));

    [Fact]
    public async Task A_marked_command_makes_exactly_one_append_with_crm_values_and_no_mailbox()
    {
        using var provider = Build();

        var response = await Send(provider, Update());

        Assert.True(response.IsSuccessful);
        var (request, body) = Assert.Single(_platform.Calls);
        Assert.Equal("http://platform.test:5057/api/internal/audit/append", request.RequestUri!.ToString());
        Assert.Equal(Key, Assert.Single(request.Headers.GetValues(PlatformAuditForwarder.InternalApiKeyHeader)));

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(nameof(UpdateVisitNotificationSettingsCommand), json.GetProperty("requestType").GetString());
        Assert.Equal(Tenant, json.GetProperty("targetTenantId").GetGuid());
        Assert.Equal((int)AuditCategory.Crm, json.GetProperty("category").GetInt32());
        Assert.Equal((int)AuditOperation.Update, json.GetProperty("operation").GetInt32());
        Assert.Equal((int)AuditOutcome.Succeeded, json.GetProperty("outcome").GetInt32());
        Assert.Equal(VisitNotificationSettingsHandlers.AuditEntity, json.GetProperty("entityType").GetString());
        Assert.Equal(CrmAuditForwarding.SourceService, json.GetProperty("sourceService").GetString());
        Assert.Equal("MOD-0155", json.GetProperty("sourceModule").GetString());
        var changed = json.GetProperty("metadata").GetProperty(AuditDetails.ChangedFieldsKey).EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "PvEmail", "OverdueReminders" }, changed);
        Assert.DoesNotContain("pv.desk@example.test", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_marked_command_is_recorded_once_as_failed_with_its_code()
    {
        using var provider = Build();

        var response = await Send(provider, Update(expectedVersion: 7)); // nothing stored yet: a stale version → 409

        Assert.False(response.IsSuccessful);
        var json = JsonDocument.Parse(Assert.Single(_platform.Calls).Body).RootElement;
        Assert.Equal((int)AuditOutcome.Failed, json.GetProperty("outcome").GetInt32());
        Assert.Equal(409, json.GetProperty("metadata").GetProperty(AuditDetails.StatusCodeKey).GetInt32());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("metadata").GetProperty(AuditDetails.RefusalCodeKey).GetString()));
    }

    [Fact]
    public async Task An_unmarked_command_and_a_query_make_no_append()
    {
        using var provider = Build();

        await Send(provider, new UnmarkedProbeCommand());
        await Send(provider, new GetVisitNotificationSettingsQuery());

        Assert.Empty(_platform.Calls);
    }

    [Fact]
    public async Task Unconfigured_crm_makes_no_call_but_counts_the_skipped_record()
    {
        using var provider = Build(baseUrl: null, key: null);

        var response = await Send(provider, Update());

        Assert.True(response.IsSuccessful); // the business command is never blocked by its audit record
        Assert.Empty(_platform.Calls);
        Assert.Equal(1, provider.GetRequiredService<AuditForwardingCounters>().Skipped);
    }

    [Fact]
    public void The_eight_recent_crm_write_commands_carry_the_shared_marker()
    {
        Type[] marked =
        [
            typeof(Features.VisitQr.IssueVisitQrCommand), typeof(Features.VisitQr.RevokeVisitQrCommand),
            typeof(Features.VisitQr.GetVisitQrPayloadCommand), typeof(Features.VisitQr.BulkIssueVisitQrCommand),
            typeof(Features.VisitQr.BulkVisitQrPayloadCommand), typeof(UpsertVisitEvidencePolicyScopeCommand),
            typeof(ArchiveVisitEvidencePolicyScopeCommand), typeof(UpdateVisitNotificationSettingsCommand)
        ];

        Assert.All(marked, type =>
        {
            Assert.True(typeof(IAuditableCommand).IsAssignableFrom(type), type.Name);
            Assert.True(typeof(IAuditDetailsProvider).IsAssignableFrom(type), type.Name);
        });
    }

    [Fact]
    public void A_payload_print_never_puts_the_payload_in_the_record()
    {
        var command = new Features.VisitQr.GetVisitQrPayloadCommand(Guid.NewGuid(), "a4", 2);
        var details = new AuditDetails();
        var payload = new Features.VisitQr.VisitQrPayloadDto(command.AccountId, "Eczane", "ACC-1", 3, "SIGNED-PAYLOAD-VALUE", DateTimeOffset.UtcNow);

        command.DescribeAuditDetails(details, Response<Features.VisitQr.VisitQrPayloadDto>.Success(payload));

        Assert.Equal(3, details.ObjectVersion);
        Assert.DoesNotContain(details.Metadata.Values, value => Equals(value, "SIGNED-PAYLOAD-VALUE"));
        Assert.Equal(AuditOperation.Export, command.DescribeAudit().Operation);
        Assert.Equal(command.AccountId, command.DescribeAudit().EntityId);
    }

    public sealed record UnmarkedProbeCommand : IRequest<Response<int>>;

    public sealed class UnmarkedProbeHandler : IRequestHandler<UnmarkedProbeCommand, Response<int>>
    {
        public Task<Response<int>> Handle(UnmarkedProbeCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Response<int>.Success(1));
    }

    private sealed class CapturingPlatform : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
