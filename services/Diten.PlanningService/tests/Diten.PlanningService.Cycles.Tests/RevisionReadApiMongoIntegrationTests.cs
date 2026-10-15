using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private static readonly IConfiguration ReadConfiguration =
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DemandPlanning:SnapshotCursorSigningKey"] =
                    "MOD0188-test-only-cursor-signing-key-32-bytes"
            }).Build();

    private static RevisionReadController ReadController(
        DemandRevisionDraft draft, PublishedRevisionMongoStore published,
        DemandPlanningMongoContext context, Guid actor,
        IInternalRevisionStatusAuthority? statusAuthority = null,
        IInternalSnapshotReadAuthority? contentAuthority = null,
        IInternalInvalidatedHistoryAuthority? auditAuthority = null,
        Guid? tenantOverride = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
            typeof(GetDemandRevisionStatusHandler).Assembly));
        services.AddSingleton<TimeProvider>(new TestClock(Now.AddHours(1)));
        services.AddSingleton<IInternalAuthoritativeRevisionStatusReader>(
            new AuthoritativeRevisionStatusReader(context,
                statusAuthority ?? new StatusAuthority(draft)));
        services.AddSingleton<IInternalPublishedSnapshotReader>(
            new PublishedSnapshotPageReader(published,
                contentAuthority ?? new ReadAuthority([draft]),
                ReadConfiguration, new TestClock(Now.AddHours(1))));
        services.AddSingleton<IInternalInvalidatedHistoryReader>(
            new InvalidatedHistoryReader(published,
                auditAuthority ?? new InvalidationAuthority(draft),
                ReadConfiguration, new TestClock(Now.AddHours(1)), context));
        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Items["DemandPlanning.TenantId"] = tenantOverride ?? draft.TenantId;
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, actor.ToString("D")),
            new Claim("permission", "demand.plans.consume"),
            new Claim("permission", "demand.audit.read")
        ], "fixture"));
        return new RevisionReadController(provider.GetRequiredService<ISender>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static Response<T> ReadResponse<T>(IActionResult action,
        int expectedStatus)
    {
        var result = Assert.IsAssignableFrom<ObjectResult>(action);
        Assert.Equal(expectedStatus, result.StatusCode);
        return Assert.IsType<Response<T>>(result.Value);
    }

    [Fact]
    public void V2ReadApi_RoutesPermissionsAndWireEnums_AreDistinct()
    {
        var type = typeof(RevisionReadController);
        Assert.Equal("api/v2/demand/revisions",
            type.GetCustomAttribute<RouteAttribute>()?.Template);
        foreach (var (action, route, permission) in new[]
        {
            ("Status", "{revisionId:guid}/status", "demand.plans.consume"),
            ("Manifest", "{revisionId:guid}/manifest", "demand.plans.consume"),
            ("Rows", "{revisionId:guid}/rows", "demand.plans.consume"),
            ("AuditSnapshot", "{revisionId:guid}/audit-snapshot", "demand.audit.read"),
            ("AuditRows", "{revisionId:guid}/audit-rows", "demand.audit.read")
        })
        {
            var method = Assert.IsAssignableFrom<MethodInfo>(type.GetMethod(action));
            Assert.Equal(route, method.GetCustomAttribute<HttpGetAttribute>()?.Template);
            Assert.Equal(permission,
                method.GetCustomAttribute<HasPermissionAttribute>()?.Policy);
        }
        var status = new RevisionStatusView("v2", Guid.NewGuid(), Guid.NewGuid(),
            "2026-10-05", Guid.NewGuid(), Guid.NewGuid(), "Published", 3,
            "Unknown", Now, new RevisionScopeSummaryView(0, 0, []),
            new RevisionActorTraceView(Guid.NewGuid(), Now, [],
                Guid.NewGuid(), Now, Guid.NewGuid(), Now), null);
        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions(
            JsonSerializerDefaults.Web));
        Assert.Contains("\"state\":\"Published\"", json);
        Assert.DoesNotContain("\"invalidation\"", json);
    }

    [ManualDraftMongoFact]
    public async Task V2ReadApi_PublishedStatusManifestPages_AndScopeLoss()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var api = ReadController(draft, published, context, actor);
        var status = ReadResponse<RevisionStatusView>(await api.Status(draft.Id,
            draft.LegalEntityId, default), 200).Data!;
        Assert.Equal("Published", status.State);
        Assert.Equal("Unknown", status.IntegrityState);
        Assert.Equal(1, status.ScopeSummary.SelectedSeriesCount);
        Assert.Equal(draft.CreatedBy, status.ActorTrace.PreparedBy);
        Assert.Equal(draft.CreatedAt, status.ActorTrace.PreparedAt);
        Assert.NotEqual(status.ActorTrace.PublishedAt,
            status.ActorTrace.PreparedAt);
        var manifest = ReadResponse<RevisionManifestView>(await api.Manifest(
            draft.Id, draft.LegalEntityId, default), 200).Data!;
        Assert.Equal(52, manifest.Calendar.Weeks.Count);
        Assert.Equal(52, manifest.ExpectedRowCount);
        Assert.Equal("Published", manifest.State);
        Assert.Equal(draft.CreatedAt, manifest.ActorTrace.PreparedAt);
        var first = ReadResponse<RevisionRowPageView>(await api.Rows(draft.Id,
            draft.LegalEntityId, 10, null, default), 200).Data!;
        Assert.Equal(10, first.ReturnedRowCount);
        Assert.NotNull(first.NextCursor);
        Assert.Equal("0", first.Items[0].GrossQuantityBaseUom);
        Assert.Equal("Manual", first.Items[0].Origin);
        Assert.False(string.IsNullOrWhiteSpace(first.Items[0].ManualReason));
        var second = ReadResponse<RevisionRowPageView>(await api.Rows(draft.Id,
            draft.LegalEntityId, 10, first.NextCursor, default), 200).Data!;
        Assert.Equal(11, second.Items[0].WeekNumber);
        var cursorParts = first.NextCursor!.Split('.');
        var tampered = cursorParts[0] + "." +
            (cursorParts[1][0] == 'A' ? 'B' : 'A') + cursorParts[1][1..];
        ReadResponse<RevisionRowPageView>(await api.Rows(draft.Id,
            draft.LegalEntityId, 10, tampered, default), 409);

        var scopeLost = ReadController(draft, published, context, actor,
            statusAuthority: new StatusAuthority(draft, inScope: false),
            contentAuthority: new ReadAuthority([draft], inScope: false));
        ReadResponse<RevisionStatusView>(await scopeLost.Status(draft.Id,
            draft.LegalEntityId, default), 404);
        ReadResponse<RevisionManifestView>(await scopeLost.Manifest(draft.Id,
            draft.LegalEntityId, default), 404);
        ReadResponse<RevisionRowPageView>(await scopeLost.Rows(draft.Id,
            draft.LegalEntityId, 10, first.NextCursor, default), 404);
        ReadResponse<RevisionStatusView>(await api.Status(draft.Id,
            Guid.NewGuid(), default), 404);
        var otherTenant = ReadController(draft, published, context, actor,
            tenantOverride: Guid.NewGuid());
        ReadResponse<RevisionStatusView>(await otherTenant.Status(draft.Id,
            draft.LegalEntityId, default), 404);
    }

    [ManualDraftMongoFact]
    public async Task V2ReadApi_SupersededAndInvalidated_StatusOnly_SeparateAudit()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var replacement = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, Guid.NewGuid())).Outcome);
        var api = ReadController(draft, published, context, actor);
        Assert.Equal("Superseded", ReadResponse<RevisionStatusView>(
            await api.Status(draft.Id, draft.LegalEntityId, default), 200).Data!.State);
        ReadResponse<RevisionManifestView>(await api.Manifest(draft.Id,
            draft.LegalEntityId, default), 409);
        ReadResponse<RevisionRowPageView>(await api.Rows(draft.Id,
            draft.LegalEntityId, 10, null, default), 409);
        ReadResponse<InvalidatedAuditSnapshotView>(await api.AuditSnapshot(
            draft.Id, draft.LegalEntityId, default), 409);

        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context),
                draft, stateVersion: 4)).Outcome);
        var invalidStatus = ReadResponse<RevisionStatusView>(
            await api.Status(draft.Id, draft.LegalEntityId, default), 200).Data!;
        Assert.Equal("Invalidated", invalidStatus.State);
        Assert.Equal("ContentIntegrity", invalidStatus.Invalidation!.ImpactCode);
        Assert.False(string.IsNullOrWhiteSpace(
            invalidStatus.Invalidation.EvidenceReference));
        ReadResponse<RevisionManifestView>(await api.Manifest(draft.Id,
            draft.LegalEntityId, default), 409);
        var audit = ReadResponse<InvalidatedAuditSnapshotView>(
            await api.AuditSnapshot(draft.Id, draft.LegalEntityId, default), 200).Data!;
        Assert.Contains("kullanılamaz", audit.Warning);
        Assert.Equal("Invalidated", audit.Manifest.State);
        Assert.NotNull(audit.Manifest.Invalidation);
        Assert.False(string.IsNullOrWhiteSpace(audit.Manifest.Invalidation!.EvidenceReference));
        var page = ReadResponse<InvalidatedAuditRowPageView>(await api.AuditRows(
            draft.Id, draft.LegalEntityId, 7, null, default), 200).Data!;
        Assert.Equal("Invalidated", page.HistoricalState);
        Assert.Contains("kullanılamaz", page.Warning);
        Assert.Equal(7, page.Page.ReturnedRowCount);
        Assert.NotNull(page.Page.NextCursor);
        var next = ReadResponse<InvalidatedAuditRowPageView>(await api.AuditRows(
            draft.Id, draft.LegalEntityId, 7, page.Page.NextCursor, default), 200).Data!;
        Assert.Equal(8, next.Page.Items[0].WeekNumber);
        var otherActor = ReadController(draft, published, context, Guid.NewGuid());
        ReadResponse<InvalidatedAuditRowPageView>(await otherActor.AuditRows(
            draft.Id, draft.LegalEntityId, 7, page.Page.NextCursor, default), 409);
        var lostAuditScope = ReadController(draft, published, context, actor,
            auditAuthority: new InvalidationAuthority(draft, scopeVerified: false));
        ReadResponse<InvalidatedAuditRowPageView>(await lostAuditScope.AuditRows(
            draft.Id, draft.LegalEntityId, 7, page.Page.NextCursor, default), 404);
        ReadResponse<InvalidatedAuditRowPageView>(await api.AuditRows(
            draft.Id, Guid.NewGuid(), 7, page.Page.NextCursor, default), 404);
        ReadResponse<InvalidatedAuditRowPageView>(await api.AuditRows(
            draft.Id, draft.LegalEntityId, 201, null, default), 409);
    }

    [ManualDraftMongoFact]
    public async Task V2ReadApi_PermissionOutageAndCorruptSnapshot_FailClosed()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var denied = ReadController(draft, published, context, actor,
            statusAuthority: new StatusAuthority(draft, permitted: false),
            contentAuthority: new ReadAuthority([draft], hasPermission: false),
            auditAuthority: new InvalidationAuthority(draft, permitted: false));
        ReadResponse<RevisionStatusView>(await denied.Status(draft.Id,
            draft.LegalEntityId, default), 403);
        ReadResponse<RevisionManifestView>(await denied.Manifest(draft.Id,
            draft.LegalEntityId, default), 403);
        ReadResponse<InvalidatedAuditSnapshotView>(await denied.AuditSnapshot(
            draft.Id, draft.LegalEntityId, default), 403);
        var outage = ReadController(draft, published, context, actor,
            statusAuthority: new StatusAuthority(draft, sourceAvailable: false),
            contentAuthority: new ReadAuthority([draft], sourceAvailable: false),
            auditAuthority: new InvalidationAuthority(draft, sourceAvailable: false));
        ReadResponse<RevisionStatusView>(await outage.Status(draft.Id,
            draft.LegalEntityId, default), 503);
        ReadResponse<RevisionManifestView>(await outage.Manifest(draft.Id,
            draft.LegalEntityId, default), 503);
        ReadResponse<InvalidatedAuditSnapshotView>(await outage.AuditSnapshot(
            draft.Id, draft.LegalEntityId, default), 503);

        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        part.Rows[0] = part.Rows[0] with { Quantity = 900m };
        await context.PublishedRevisionParts.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == part.Id, part);
        var api = ReadController(draft, published, context, actor);
        Assert.Equal("Invalidated", ReadResponse<RevisionStatusView>(
            await api.Status(draft.Id, draft.LegalEntityId, default), 200).Data!.State);
        ReadResponse<InvalidatedAuditSnapshotView>(await api.AuditSnapshot(
            draft.Id, draft.LegalEntityId, default), 503);
        ReadResponse<InvalidatedAuditRowPageView>(await api.AuditRows(
            draft.Id, draft.LegalEntityId, 10, null, default), 503);
        var hidden = ReadController(draft, published, context, actor,
            auditAuthority: new InvalidationAuthority(draft, scopeVerified: false));
        ReadResponse<InvalidatedAuditSnapshotView>(await hidden.AuditSnapshot(
            draft.Id, draft.LegalEntityId, default), 404);
    }

    [ManualDraftMongoFact]
    public async Task V2ReadApi_PreparationTraceTamper_FailsClosed()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: false);
        var manifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        manifest.PreparedAt = manifest.PreparedAt.AddMinutes(1);
        await context.PublishedRevisionManifests.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == manifest.Id, manifest);
        var api = ReadController(draft, published, context, Guid.NewGuid());
        ReadResponse<RevisionStatusView>(await api.Status(draft.Id,
            draft.LegalEntityId, default), 503);
        ReadResponse<RevisionManifestView>(await api.Manifest(draft.Id,
            draft.LegalEntityId, default), 503);
    }

    [ManualDraftMongoFact]
    public async Task V2ReadApi_CorruptPublishedPart_NeverReturnsPartialPage()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: false);
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        part.Rows.RemoveAt(0);
        await context.PublishedRevisionParts.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == part.Id, part);
        var actor = Guid.NewGuid();
        var api = ReadController(draft, published, context, actor);
        Assert.Equal("Published", ReadResponse<RevisionStatusView>(
            await api.Status(draft.Id, draft.LegalEntityId, default), 200).Data!.State);
        ReadResponse<RevisionManifestView>(await api.Manifest(draft.Id,
            draft.LegalEntityId, default), 503);
        ReadResponse<RevisionRowPageView>(await api.Rows(draft.Id,
            draft.LegalEntityId, 10, null, default), 503);
        var hidden = ReadController(draft, published, context, actor,
            statusAuthority: new StatusAuthority(draft, inScope: false),
            contentAuthority: new ReadAuthority([draft], inScope: false));
        ReadResponse<RevisionStatusView>(await hidden.Status(draft.Id,
            draft.LegalEntityId, default), 404);
        ReadResponse<RevisionManifestView>(await hidden.Manifest(draft.Id,
            draft.LegalEntityId, default), 404);
    }
}
