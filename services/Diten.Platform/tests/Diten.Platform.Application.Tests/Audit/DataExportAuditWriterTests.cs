using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// BL-347 — THE SHARED TENANT-SIDE EXPORT AUDIT WRITER.
///
/// <para><b>⚠ THE PRINCIPAL IS REAL.</b> Every test builds claims and hands them to the production readers
/// (<see cref="JwtTenantAuthorizationContext"/>, <see cref="CurrentUserContext"/>). A double that simply answered
/// "tenant user" would prove the writer copies a value, not that the value is the token's.</para>
///
/// <para>The entries are shaped like MOD-0357 S12's meeting report export on purpose — the second caller this
/// writer was designed for — while the work report's own use is guarded in <c>WorkReportExportTests</c> and on a
/// real database in <c>WorkReportExportAuditTrailMongoTests</c>.</para>
/// </summary>
public sealed class DataExportAuditWriterTests
{
    private static readonly Guid Tenant = Guid.Parse("34734734-0000-4000-8000-000000000001");
    private static readonly Guid User = Guid.Parse("34734734-0000-4000-8000-000000000002");

    private static DataExportAuditEntry Entry(
        IReadOnlyDictionary<string, string?>? filters = null,
        string? dataset = null) => new(
        "MOD-0357",
        "Meetings.MeetingReportExportQuery",
        "MeetingReport",
        "csv",
        7,
        filters ?? new Dictionary<string, string?>(),
        dataset,
        "corr-347");

    private static TenantContext TenantOf(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return context;
    }

    private static (DataExportAuditWriter Writer, CapturingAuditService Audit) Build(
        IHttpContextAccessor principal,
        ITenantContext tenant,
        CapturingAuditService? audit = null)
    {
        audit ??= new CapturingAuditService(AuditAppendResult.Queued("audit:export"));
        var writer = new DataExportAuditWriter(
            audit,
            new JwtTenantAuthorizationContext(principal, new FakeDataScopeResolver()),
            new CurrentUserContext(principal),
            tenant,
            NullLogger<DataExportAuditWriter>.Instance);

        return (writer, audit);
    }

    // ── WHO, WHICH TENANT, NEVER PLATFORM-GLOBAL ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tenant_users_export_is_recorded_as_that_user_under_that_tenant_and_never_platform_global()
    {
        // ⚠ SABOTAGE 1's GUARD: stamping PlatformAdministrator (or platform-global) turns this red.
        var (writer, audit) = Build(Principal("tenant_user", User), TenantOf(Tenant));

        var result = await writer.RecordAsync(Entry(), CancellationToken.None);

        Assert.True(result.IsRecorded);
        Assert.Equal(AuditActorType.TenantUser, result.ActorType);
        Assert.Equal(AuditAppendStatus.Queued, result.AppendStatus);

        var request = Assert.Single(audit.Requests);
        Assert.Equal(AuditActorType.TenantUser, request.ActorType);
        Assert.Equal(User, request.ActorId);
        Assert.Equal(RawEmail, request.ActorEmail); // masked by AuditService on the way to the outbox
        Assert.Equal(RawDisplayName, request.ActorDisplayName);
        Assert.Equal(Tenant, request.TargetTenantId);
        Assert.False(request.IsPlatformGlobal);
        Assert.False(request.IsMetaAudit);
        Assert.Equal(AuditCategory.DataExport, request.Category);
        Assert.Equal(AuditOperation.Export, request.Operation);
        Assert.Equal(AuditOutcome.Succeeded, request.Outcome);
        Assert.Equal("MOD-0357", request.SourceModule);
        Assert.Equal("Meetings.MeetingReportExportQuery", request.RequestType);
        Assert.Equal("MeetingReport", request.EntityType);
        Assert.Equal("Diten.Platform", request.SourceService);
        Assert.NotEqual(Guid.Empty, request.CorrelationId);
    }

    [Theory]
    [InlineData("tenant_user", AuditActorType.TenantUser)]
    [InlineData("TENANT_USER", AuditActorType.TenantUser)]
    [InlineData("platform_admin", AuditActorType.PlatformAdministrator)]
    [InlineData("partner_admin", AuditActorType.PartnerAdministrator)]
    public async Task The_actor_type_is_the_tokens_and_the_record_stays_with_the_tenant_whose_data_left(
        string claim,
        AuditActorType expected)
    {
        var (writer, audit) = Build(Principal(claim, User), TenantOf(Tenant));

        var result = await writer.RecordAsync(Entry(), CancellationToken.None);

        Assert.True(result.IsRecorded);
        var request = Assert.Single(audit.Requests);
        Assert.Equal(expected, request.ActorType);
        Assert.Equal(Tenant, request.TargetTenantId);
        Assert.False(request.IsPlatformGlobal);
    }

    [Fact]
    public async Task Two_exports_are_two_records_even_under_one_http_correlation_id()
    {
        // The idempotency key is built from the correlation id: reusing the request's would de-duplicate a
        // retried download into the first one's record, and the second file would leave no trace.
        var (writer, audit) = Build(Principal("tenant_user", User), TenantOf(Tenant));

        await writer.RecordAsync(Entry(), CancellationToken.None);
        await writer.RecordAsync(Entry(), CancellationToken.None);

        Assert.Equal(2, audit.Requests.Select(request => request.CorrelationId).Distinct().Count());
    }

    // ── UNATTRIBUTABLE → REFUSED, BEFORE ANYTHING IS WRITTEN ────────────────────────────────────────────────

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("service")]
    [InlineData("system")]
    [InlineData("admin")]
    public async Task A_principal_without_a_recognised_actor_type_is_refused_before_the_audit_service_is_asked(
        string? claim)
    {
        var (writer, audit) = Build(Principal(claim, User), TenantOf(Tenant));

        var result = await writer.RecordAsync(Entry(), CancellationToken.None);

        Assert.False(result.IsRecorded);
        Assert.Equal(AuditActorType.Unknown, result.ActorType);
        Assert.Null(result.AppendStatus);
        Assert.Empty(audit.Requests);
    }

    [Fact]
    public async Task An_unauthenticated_principal_is_refused()
    {
        var (writer, audit) = Build(Principal("tenant_user", User, authenticated: false), TenantOf(Tenant));

        Assert.False((await writer.RecordAsync(Entry(), CancellationToken.None)).IsRecorded);
        Assert.Empty(audit.Requests);
    }

    [Fact]
    public async Task A_principal_without_a_user_id_is_refused()
    {
        var (writer, audit) = Build(Principal("tenant_user", userId: null), TenantOf(Tenant));

        Assert.False((await writer.RecordAsync(Entry(), CancellationToken.None)).IsRecorded);
        Assert.Empty(audit.Requests);
    }

    [Fact]
    public async Task Without_a_tenant_nothing_is_recorded_and_nothing_is_filed_under_the_platform_tenant()
    {
        var unresolved = new TenantContext();
        var platformPinned = new TenantContext();
        platformPinned.SetPlatformContext(Guid.Empty);

        foreach (var tenant in new ITenantContext[] { unresolved, platformPinned })
        {
            var (writer, audit) = Build(Principal("tenant_user", User), tenant);

            Assert.False((await writer.RecordAsync(Entry(), CancellationToken.None)).IsRecorded);
            Assert.Empty(audit.Requests);
        }
    }

    // ── ONLY A QUEUED RECORD IS A RECORD ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(AuditAppendStatus.Duplicate))]
    [InlineData(nameof(AuditAppendStatus.Rejected))]
    [InlineData(nameof(AuditAppendStatus.EnqueueFailed))]
    [InlineData(nameof(AuditAppendStatus.SkippedRecursion))]
    public async Task Any_answer_other_than_queued_is_not_a_record(string status)
    {
        var answer = status switch
        {
            nameof(AuditAppendStatus.Duplicate) => AuditAppendResult.Duplicate("audit:dup"),
            nameof(AuditAppendStatus.Rejected) => AuditAppendResult.Rejected("rejected"),
            nameof(AuditAppendStatus.EnqueueFailed) => AuditAppendResult.EnqueueFailed("audit:k", "down"),
            _ => AuditAppendResult.SkippedRecursion("recursion")
        };
        var (writer, _) = Build(Principal("tenant_user", User), TenantOf(Tenant), new CapturingAuditService(answer));

        var result = await writer.RecordAsync(Entry(), CancellationToken.None);

        Assert.False(result.IsRecorded);
        Assert.Equal(Enum.Parse<AuditAppendStatus>(status), result.AppendStatus);
    }

    [Fact]
    public async Task A_throwing_audit_service_is_answered_as_not_recorded_rather_than_thrown()
    {
        var failing = new CapturingAuditService(AuditAppendResult.Queued("never"), new TimeoutException("outbox down"));
        var (writer, _) = Build(Principal("tenant_user", User), TenantOf(Tenant), failing);

        var result = await writer.RecordAsync(Entry(), CancellationToken.None);

        Assert.False(result.IsRecorded);
        Assert.Contains("TimeoutException", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        var cancelled = new CapturingAuditService(AuditAppendResult.Queued("never"), new OperationCanceledException());
        var (writer, _) = Build(Principal("tenant_user", User), TenantOf(Tenant), cancelled);

        await Assert.ThrowsAsync<OperationCanceledException>(() => writer.RecordAsync(Entry(), CancellationToken.None));
    }

    // ── THE METADATA — THE S12 SHAPE ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_metadata_carries_format_rows_dataset_and_only_the_filters_in_force_with_no_person_in_them()
    {
        var organizer = Guid.Parse("0f0f0f0f-1234-4567-89ab-0f0f0f0f0f0f");
        var (writer, audit) = Build(Principal("tenant_user", User), TenantOf(Tenant));

        await writer.RecordAsync(
            Entry(
                new Dictionary<string, string?>
                {
                    ["from"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["meetingType"] = "MANAGEMENT_REVIEW",
                    ["organizer"] = DataExportFilterSummary.Person(organizer),
                    ["attendee"] = DataExportFilterSummary.Person(null),
                    ["status"] = null
                },
                dataset: "actions"),
            CancellationToken.None);

        var metadata = Assert.Single(audit.Requests).Metadata;
        Assert.Equal("csv", metadata["format"]);
        Assert.Equal(7, metadata["rowCount"]);
        Assert.Equal("actions", metadata["dataset"]);
        Assert.Equal("corr-347", metadata["requestCorrelationId"]);

        var filters = AsDictionary(metadata["filters"]);
        Assert.Equal(DataExportFilterSummary.Applied, filters["organizer"]);
        Assert.Equal("MANAGEMENT_REVIEW", filters["meetingType"]);
        Assert.False(filters.ContainsKey("attendee"));
        Assert.False(filters.ContainsKey("status"));
        Assert.DoesNotContain(organizer.ToString(), Flatten(metadata), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingAuditService(AuditAppendResult answer, Exception? throws = null) : IAuditService
    {
        public List<AuditAppendRequest> Requests { get; } = [];

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            if (throws is not null)
            {
                throw throws;
            }

            return Task.FromResult(answer);
        }
    }
}
