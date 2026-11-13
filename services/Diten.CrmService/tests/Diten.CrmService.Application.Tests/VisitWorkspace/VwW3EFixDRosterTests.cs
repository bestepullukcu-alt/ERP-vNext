using System.Reflection;
using System.Security.Claims;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W3E · W3E-E4R2-FIX-d (U-7) — the settings page's paged REP ROSTER on the PRODUCTION handler and controller: pages
/// with the total (25 a page, the 26th rep on page 2, the last page short), the picker's q filter, a stable order (name,
/// then id), the page size 1–100 (0 / 101 → 400), the tenant's reps only, no read key → 403 with nothing asked; and the
/// picker read beside it is unchanged (at most 20, the same shape).
/// </summary>
public sealed class VwW3EFixDRosterTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000d1");
    private static readonly Guid OtherTenant = Guid.Parse("97c50000-0000-0000-0000-0000000000d2");

    /// <summary>26 reps of the tenant (two with the SAME name — the id breaks the tie) + one of another tenant.</summary>
    private static TenantRoster Roster()
    {
        var reps = Enumerable.Range(1, 24).Select(i => new EvidenceRepDto("rep-" + i.ToString("00"), "Temsilci " + i.ToString("00"))).ToList();
        reps.Add(new EvidenceRepDto("rep-z", "Ayşe Yılmaz"));
        reps.Add(new EvidenceRepDto("rep-a", "Ayşe Yılmaz"));
        return new TenantRoster(new Dictionary<Guid, IReadOnlyList<EvidenceRepDto>>
        {
            [Tenant] = reps,
            [OtherTenant] = [new EvidenceRepDto("foreign-1", "Başka Kiracı")]
        });
    }

    private static VisitEvidencePolicyReadHandlers Handler(TenantRoster roster, Guid? tenant = null)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(tenant ?? Tenant);
        return new VisitEvidencePolicyReadHandlers(ctx, new NoChanges(), new VisitEvidencePolicyResolver(new FakeVisitEvidencePolicyRepository()), roster);
    }

    [Fact]
    public async Task R1_pages_carry_the_total_25_a_page_the_26th_rep_on_page_2_and_a_short_last_page()
    {
        var h = Handler(Roster());
        var first = (await h.Handle(new ListVisitEvidenceRepRosterQuery(null), default)).Data!;
        Assert.Equal((26L, 1, 25, 25), (first.Total, first.Page, first.PageSize, first.Items.Count));
        var second = (await h.Handle(new ListVisitEvidenceRepRosterQuery(null, 2), default)).Data!;
        Assert.Equal((26L, 1), (second.Total, second.Items.Count));                                     // the 26th: never cut
        Assert.Equal("rep-24", second.Items.Single().ResourceId);
        var tens = (await h.Handle(new ListVisitEvidenceRepRosterQuery(null, 3, 10), default)).Data!;
        Assert.Equal((26L, 6), (tens.Total, tens.Items.Count));                                         // the last page short
        Assert.Empty((await h.Handle(new ListVisitEvidenceRepRosterQuery(null, 9, 10), default)).Data!.Items);
        // every rep exactly once across the pages
        var all = new List<string>();
        for (var p = 1; p <= 3; p++) all.AddRange((await h.Handle(new ListVisitEvidenceRepRosterQuery(null, p, 10), default)).Data!.Items.Select(r => r.ResourceId));
        Assert.Equal(26, all.Distinct().Count());
    }

    [Fact]
    public async Task R2_the_order_is_stable_by_name_then_id_and_q_filters_like_the_picker()
    {
        var h = Handler(Roster());
        var page = (await h.Handle(new ListVisitEvidenceRepRosterQuery(null, 1, 3), default)).Data!;
        Assert.Equal(new[] { "rep-a", "rep-z", "rep-01" }, page.Items.Select(r => r.ResourceId).ToArray()); // the same name: the id decides
        var ayse = (await h.Handle(new ListVisitEvidenceRepRosterQuery("ayş"), default)).Data!;
        Assert.Equal(2L, ayse.Total);
        Assert.Equal(new[] { "rep-a", "rep-z" }, ayse.Items.Select(r => r.ResourceId).ToArray());
        var byId = (await h.Handle(new ListVisitEvidenceRepRosterQuery(" REP-1 "), default)).Data!;   // trimmed; the id, case-insensitive
        Assert.Equal(10L, byId.Total);                                                                   // rep-10 … rep-19
        Assert.True(VisitEvidencePolicyReadHandlers.Matches(new EvidenceRepDto("x", "Murat"), "mur"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public async Task R3_a_page_size_outside_1_to_100_is_refused(int size)
    {
        var roster = Roster();
        var r = await Handler(roster).Handle(new ListVisitEvidenceRepRosterQuery(null, 1, size), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(0, roster.Calls);                                                                   // refused before the read
        Assert.Equal(100, (await Handler(Roster()).Handle(new ListVisitEvidenceRepRosterQuery(null, 1, 100), default)).Data!.PageSize);
        Assert.Equal(400, (await Handler(Roster()).Handle(new ListVisitEvidenceRepRosterQuery(null, 0, 10), default)).StatusCode);
    }

    [Fact]
    public async Task R4_only_the_tenants_reps_and_the_picker_read_is_unchanged()
    {
        var roster = Roster();
        var mine = (await Handler(roster).Handle(new ListVisitEvidenceRepRosterQuery("Başka"), default)).Data!;
        Assert.Equal(0L, mine.Total);
        var theirs = (await Handler(roster, OtherTenant).Handle(new ListVisitEvidenceRepRosterQuery(null), default)).Data!;
        Assert.Equal((1L, "foreign-1"), (theirs.Total, theirs.Items.Single().ResourceId));
        Assert.All(roster.Asked, t => Assert.Contains(t, new[] { Tenant, OtherTenant }));

        // the picker: still at most 20, still a plain list of { resourceId, displayName }
        var picker = (await Handler(Roster()).Handle(new ListVisitEvidenceRepsQuery(null), default)).Data!;
        Assert.Equal(20, picker.Count);
        Assert.Equal(20, VisitEvidencePolicyReadHandlers.MaxReps);
        Assert.IsAssignableFrom<IReadOnlyList<EvidenceRepDto>>(picker);
    }

    [Fact]
    public async Task R5_the_endpoint_needs_the_policy_read_or_manage_key_and_routes_page_and_q()
    {
        var route = typeof(VisitEvidenceSettingsController).GetMethod(nameof(VisitEvidenceSettingsController.RepRoster))!.GetCustomAttribute<HttpGetAttribute>()!.Template;
        Assert.Equal("api/crm/visit-evidence-policy/reps/roster", route);
        Assert.Equal("api/crm/visit-evidence-policy/reps", typeof(VisitEvidenceSettingsController).GetMethod(nameof(VisitEvidenceSettingsController.Reps))!.GetCustomAttribute<HttpGetAttribute>()!.Template);

        var (none, noneMediator) = Controller();
        var refused = await none.RepRoster("x", 2, 10, default);
        Assert.Equal(403, (refused as ObjectResult)?.StatusCode ?? (refused as StatusCodeResult)?.StatusCode);
        Assert.Empty(noneMediator.Sent);

        var (reader, readerMediator) = Controller("crm.visit-evidence-policy.read");
        await reader.RepRoster("ayş", 2, 10, default);
        var sent = Assert.IsType<ListVisitEvidenceRepRosterQuery>(Assert.Single(readerMediator.Sent));
        Assert.Equal(("ayş", (int?)2, (int?)10), (sent.Q, sent.Page, sent.PageSize));
        var (manager, managerMediator) = Controller("crm.visit-evidence-policy.manage");
        await manager.RepRoster(null, null, null, default);
        Assert.Single(managerMediator.Sent);
    }

    // ── doubles ─────────────────────────────────────────────────────────────────────────────────────────────

    private static (VisitEvidenceSettingsController Controller, RecordingMediator Mediator) Controller(params string[] keys)
    {
        var proxy = DispatchProxy.Create<IMediator, RecordingMediator>();
        var mediator = (RecordingMediator)(object)proxy;
        var controller = new VisitEvidenceSettingsController(proxy)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(keys.Select(k => new System.Security.Claims.Claim("permission", k)), "test")) }
            }
        };
        return (controller, mediator);
    }

    /// <summary>An IMediator that records what it is asked and answers a successful empty roster page.</summary>
    public class RecordingMediator : DispatchProxy
    {
        public List<object> Sent { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Send" && args is { Length: > 0 } && args[0] is ListVisitEvidenceRepRosterQuery q)
            {
                Sent.Add(q);
                return Task.FromResult(Diten.CrmService.Application.Common.Models.Response<EvidenceRepRosterDto>.Success(new EvidenceRepRosterDto([], 0, 1, 25)));
            }

            throw new InvalidOperationException("unexpected mediator call " + targetMethod?.Name);
        }
    }

    private sealed class TenantRoster(Dictionary<Guid, IReadOnlyList<EvidenceRepDto>> byTenant) : IVisitEvidenceRepRoster
    {
        public int Calls { get; private set; }
        public List<Guid> Asked { get; } = new();

        public Task<IReadOnlyList<EvidenceRepDto>> ListAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
        {
            Calls++;
            Asked.Add(tenantId);
            return Task.FromResult(byTenant.GetValueOrDefault(tenantId) ?? (IReadOnlyList<EvidenceRepDto>)Array.Empty<EvidenceRepDto>());
        }
    }

    private sealed class NoChanges : IVisitEvidencePolicyChangeRepository
    {
        public Task InsertManyAsync(IReadOnlyCollection<VisitEvidencePolicyChange> rows, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<(IReadOnlyList<VisitEvidencePolicyChange> Items, long Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<VisitEvidencePolicyChange>, long)>((Array.Empty<VisitEvidencePolicyChange>(), 0));
    }
}
