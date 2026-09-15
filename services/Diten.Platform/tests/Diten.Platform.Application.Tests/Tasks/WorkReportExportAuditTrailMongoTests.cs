using System.Globalization;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;
using static Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// BL-347 — THE WORK REPORT EXPORT'S AUDIT TRAIL, ON A REAL DATABASE.
///
/// <para><b>⚠ NOTHING ON THE PATH IS A DOUBLE EXCEPT WHAT CANNOT RUN OUTSIDE A HOST.</b> The handler, the report
/// repository, the task repository, the scope source, the export audit writer, the audit service, the outbox
/// repository and the payload mapper are all production classes over MongoDB. The principal is real claims read
/// by the production readers. Doubles remain only for the org-scope resolver (the caller holds the tenant-wide
/// permission, so it is never consulted), the unit/type catalogues (labels only) and the legal-entity validator
/// (no company filter is sent).</para>
///
/// <para>Isolation is the tenant (DB-010): the shared test database, a fresh <c>TenantId</c> per harness.</para>
/// </summary>
public sealed class WorkReportExportAuditTrailMongoTests
{
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(1);

    [Fact]
    public async Task A_tenant_users_export_leaves_exactly_one_tenant_owned_DataExport_record_and_the_file_is_unchanged()
    {
        await using var mine = await MongoIntegrationHarness.CreateAsync(
            SchemaProfile.WorkflowWorkCenter, SchemaProfile.AccessGovernance);
        await using var theirs = await MongoIntegrationHarness.CreateAsync(
            SchemaProfile.WorkflowWorkCenter, SchemaProfile.AccessGovernance);

        // The assignee filter names a PERSON — the one filter value that must never reach the record.
        var person = Guid.NewGuid();
        var mineIds = new List<Guid>();
        mineIds.AddRange(await SeedAsync(mine, person, count: 2));
        mineIds.AddRange(await SeedAsync(mine, Guid.NewGuid(), count: 1));
        await SeedAsync(theirs, person, count: 3);

        var user = Guid.NewGuid();
        var principal = Principal("tenant_user", user, tenantClaim: mine.TenantId);
        var recorder = new RecordingAuditService(RealAuditService(mine, principal));
        var filter = new WorkReportFilter(AssigneeUserId: person);

        var response = await Handler(mine, principal, recorder).Handle(
            new WorkReportExportQuery(From, To, "csv", "corr-bl347", filter), CancellationToken.None);

        // ── the file: exactly the report's own rows, untouched by the audit step ──
        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));
        var expected = await Repository(mine).ExportAsync(
            new WorkReportCriteria(From, To, WorkReportScope.TenantWideScope(), WorkReportGroupBy.None, filter),
            WorkReportExportLimits.MaxRows);
        Assert.NotEmpty(expected.Rows);
        Assert.All(expected.Rows, row => Assert.Contains(row.Id, mineIds));
        Assert.Equal(expected.Rows.Count, response.Data!.RowCount);
        Assert.Equal(WorkReportExportSerializer.ToCsv(expected.Rows), response.Data.Content);

        // ── the append: asked once, accepted, tenant-owned ──
        // ⚠ SABOTAGE 2's GUARD: skipping the audit call leaves no request here and no document below.
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(AuditAppendStatus.Queued, Assert.Single(recorder.Results).Status);
        Assert.False(request.IsPlatformGlobal);
        Assert.False(request.IsMetaAudit);

        // ── the stored record: exactly one, under this tenant, and none anywhere else ──
        var record = Assert.Single(await Outbox(mine.Database).Find(m => m.TenantId == mine.TenantId).ToListAsync());
        Assert.Equal(request.CorrelationId, record.CorrelationId);
        Assert.Equal("Tasks.WorkReportExportQuery", record.RequestType);
        Assert.Equal(AuditOperation.Export, record.Operation);
        Assert.Equal(0, await Outbox(mine.Database).CountDocumentsAsync(m => m.TenantId == theirs.TenantId));
        Assert.Equal(0, await Outbox(mine.Database).CountDocumentsAsync(
            m => m.CorrelationId == request.CorrelationId && m.TenantId != mine.TenantId));

        // ── what the audit worker will write to audit_events, read by the production mapper ──
        // ⚠ SABOTAGE 1's GUARD on a real database: a PlatformAdministrator stamp fails the next line, a
        // platform-global one fails the tenant assertions above.
        var written = new AuditOutboxPayloadMapper().Map(ToProcessingItem(record), DateTimeOffset.UtcNow);
        Assert.Equal(AuditActorType.TenantUser, written.ActorType);
        Assert.Equal(user, written.ActorId);
        Assert.Equal(mine.TenantId, written.TenantId);
        Assert.Equal(mine.TenantId, written.TargetTenantId);
        Assert.Equal(AuditCategory.DataExport, written.Category);
        Assert.Equal(AuditOperation.Export, written.Operation);
        Assert.Equal(AuditOutcome.Succeeded, written.Outcome);
        Assert.Equal("WorkReport", written.EntityType);
        Assert.Equal("MOD-0024", written.SourceModule);
        Assert.False(written.IsMetaAudit);
        Assert.NotNull(written.ActorEmailMasked);
        Assert.NotNull(written.ActorDisplayNameMasked);

        Assert.Equal(expected.Rows.Count, Convert.ToInt32(written.Metadata["rowCount"], CultureInfo.InvariantCulture));
        Assert.Equal("csv", written.Metadata["format"]);
        Assert.Equal("corr-bl347", written.Metadata["requestCorrelationId"]);
        var filters = AsDictionary(written.Metadata["filters"]);
        Assert.Equal(DataExportFilterSummary.Applied, filters["assignee"]);

        // ── no personal data: not the filtered person, not the actor's raw email or name ──
        var stored = Flatten(record.Payload);
        Assert.DoesNotContain(person.ToString(), stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RawEmail, stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RawDisplayName, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_the_audit_append_fails_the_export_is_refused_and_no_file_content_is_returned()
    {
        // ⚠ SABOTAGE 3's GUARD on the real path: swallowing the failure and returning the file turns this red.
        await using var mine = await MongoIntegrationHarness.CreateAsync(
            SchemaProfile.WorkflowWorkCenter, SchemaProfile.AccessGovernance);
        await SeedAsync(mine, Guid.NewGuid(), count: 2);

        var principal = Principal("tenant_user", Guid.NewGuid(), tenantClaim: mine.TenantId);
        var unreachable = new AuditService(
            new UnreachableOutbox(),
            new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
            new AuditIdempotencyKeyBuilder(),
            new AuditRecursionGuard(),
            mine.TenantContext,
            new CurrentUserContext(principal),
            NullLogger<AuditService>.Instance);

        var response = await Handler(mine, principal, unreachable).Handle(
            new WorkReportExportQuery(From, To, "csv", "corr-bl347-down"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(DataExportAuditReasonCodes.NotRecorded, response.ReasonCode);
        Assert.Null(response.Data);
        Assert.Equal(0, await Outbox(mine.Database).CountDocumentsAsync(m => m.TenantId == mine.TenantId));
    }

    // ── the real path ──────────────────────────────────────────────────────────────────────────────────────

    private static WorkReportExportQueryHandler Handler(
        MongoIntegrationHarness harness,
        IHttpContextAccessor principal,
        IAuditService audit)
    {
        var currentUser = new CurrentUserContext(principal);
        var scope = new WorkReportScopeSource(
            new FakeDataScopeResolver(),
            currentUser,
            harness.TenantContext,
            TaskActors.Holding(TaskPermissions.WorkReportRead, TaskPermissions.WorkReportReadTenantWide),
            NullLogger<WorkReportScopeSource>.Instance);

        var exportAudit = new DataExportAuditWriter(
            audit,
            new JwtTenantAuthorizationContext(principal, new FakeDataScopeResolver()),
            currentUser,
            harness.TenantContext,
            NullLogger<DataExportAuditWriter>.Instance);

        return new WorkReportExportQueryHandler(Repository(harness), scope, exportAudit);
    }

    private static WorkReportRepository Repository(MongoIntegrationHarness harness) => new(
        harness.DbContext,
        harness.TenantContext,
        new FakeOrganizationUnitRepository(),
        new FakeTaskTypeRepository(),
        new NoCompanyFilterSent(),
        NullLogger<WorkReportRepository>.Instance);

    private static AuditService RealAuditService(MongoIntegrationHarness harness, IHttpContextAccessor principal) => new(
        new AuditOutboxRepository(harness.DbContext),
        new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
        new AuditIdempotencyKeyBuilder(),
        new AuditRecursionGuard(),
        harness.TenantContext,
        new CurrentUserContext(principal),
        NullLogger<AuditService>.Instance);

    private static async System.Threading.Tasks.Task<IReadOnlyList<Guid>> SeedAsync(
        MongoIntegrationHarness harness,
        Guid assignee,
        int count)
    {
        var tasks = new TaskItemRepository(
            harness.DbContext,
            harness.TenantContext,
            new TaskTransitionRepository(harness.DbContext, harness.TenantContext));

        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var created = await tasks.CreateAsync(new TaskItem
            {
                TenantId = harness.TenantId,
                OrganizationUnitId = Guid.NewGuid(),
                Title = $"BL-347 export row {i}",
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = assignee,
                CreatedByUserId = assignee,
                Lifecycle = TaskLifecycle.Open
            });
            ids.Add(created.Id);
        }

        return ids;
    }

    private sealed class UnreachableOutbox : IAuditOutboxWriter
    {
        public System.Threading.Tasks.Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
            => throw new TimeoutException("test: the audit outbox is unreachable");
    }

    private sealed class NoCompanyFilterSent : ILegalEntityReferenceValidator
    {
        public System.Threading.Tasks.Task<Response<LegalEntityReferenceDto>> ValidateAsync(
            Guid legalEntityId, CancellationToken ct = default)
            => throw new NotSupportedException("No company filter is sent in these tests.");
    }
}
