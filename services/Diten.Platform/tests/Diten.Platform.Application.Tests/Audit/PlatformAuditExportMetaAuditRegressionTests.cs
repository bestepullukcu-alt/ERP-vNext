using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;
using static Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// BL-347 REGRESSION — THE PLATFORM AUDIT EXPORT RECORDS EXACTLY WHAT IT RECORDED BEFORE.
///
/// <para>The tenant-side writer was built BESIDE <see cref="AuditMetaAuditWriter"/>, not into it, so the
/// platform audit export's own record must not move: a platform administrator, platform-global (filed under the
/// platform-system tenant), a meta-audit, <c>SourceModule = "Audit"</c>. Before this test nothing checked that
/// on the real path — <c>AuditPhase5ApiSurfaceTests</c> drives the handler with a capturing double and never
/// looks at the actor.</para>
///
/// <para>Real handler, real meta-audit writer, real audit service, real outbox on MongoDB; the principal is claims
/// read by <see cref="CurrentUserContext"/>; the tenant context is what <c>TenantResolutionMiddleware</c> pins on
/// an admin path (<c>SetPlatformContext(Guid.Empty)</c>). The record is found by its own correlation id, because
/// the platform-system tenant is shared by every platform-global record in the test database.</para>
/// </summary>
public sealed class PlatformAuditExportMetaAuditRegressionTests
{
    [Fact]
    public async Task The_platform_audit_export_still_records_a_platform_administrator_platform_global_meta_audit()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.AccessGovernance);

        var admin = Guid.NewGuid();
        var principal = Principal("platform_admin", admin);
        var currentUser = new CurrentUserContext(principal);
        var adminPath = new TenantContext();
        adminPath.SetPlatformContext(Guid.Empty);

        var recursionGuard = new AuditRecursionGuard();
        var recorder = new RecordingAuditService(new AuditService(
            new AuditOutboxRepository(harness.DbContext),
            new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
            new AuditIdempotencyKeyBuilder(),
            recursionGuard,
            adminPath,
            currentUser,
            NullLogger<AuditService>.Instance));

        var handler = new ExportAuditEventsHandler(
            new AuditEventRepository(harness.Database, adminPath),
            new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
            new AuditMetaAuditWriter(recorder, recursionGuard, currentUser, NullLogger<AuditMetaAuditWriter>.Instance));

        var now = DateTimeOffset.UtcNow;
        var response = await handler.Handle(
            new ExportAuditEventsQuery(new AuditExportRequest
            {
                Format = AuditExportFormats.Csv,
                FromUtc = now.AddHours(-1),
                ToUtc = now,
                Limit = 10
            }),
            CancellationToken.None);

        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(AuditAppendStatus.Queued, Assert.Single(recorder.Results).Status);
        Assert.Equal(AuditActorType.PlatformAdministrator, request.ActorType);
        Assert.True(request.IsPlatformGlobal);
        Assert.True(request.IsMetaAudit);
        Assert.Equal("Audit", request.SourceModule);
        Assert.Equal("PlatformAudit.ExportAuditEventsQuery", request.RequestType);
        Assert.Equal(AuditCategory.DataExport, request.Category);
        Assert.Equal(AuditOperation.Export, request.Operation);

        var record = Assert.Single(
            await Outbox(harness.Database).Find(m => m.CorrelationId == request.CorrelationId).ToListAsync());
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, record.TenantId);

        var written = new AuditOutboxPayloadMapper().Map(ToProcessingItem(record), now);
        Assert.Equal(AuditActorType.PlatformAdministrator, written.ActorType);
        Assert.Equal(admin, written.ActorId);
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, written.TenantId);
        Assert.True(written.IsMetaAudit);
        Assert.Equal("Audit", written.SourceModule);
        Assert.Equal(AuditCategory.DataExport, written.Category);
    }
}
