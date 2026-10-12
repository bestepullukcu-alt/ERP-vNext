using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-3C (K-7) — the product list on the PRODUCTION engine and selection handler: an approved week keeps its frozen
/// list while the draft weeks take the new pick (S-1), the per-doctor pick written through the existing selection update
/// (null keeps, [] clears, checked against MDM fail-closed), and the preview's product summaries.
/// </summary>
public sealed partial class VisitPlanningTests
{
    // ── 7 · S-1: an approved week is frozen ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_approved_weeks_products_stay_as_written_and_the_draft_weeks_take_the_new_pick()
    {
        var env = WeeklyEnv();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        Pick(env, env.DoctorA, (p1, "P1", null));

        var approved = await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);
        var atom = approved.Single(a => a.ContactId == env.DoctorA);
        Assert.Equal((p1, PlannedVisitContentItemSources.RepPick), (atom.ContentItems.Single().ProductId, atom.ContentItems.Single().Source));
        var frozen = atom.ContentItems.Select(i => (i.ProductId, i.Source, i.Order)).ToList();

        Pick(env, env.DoctorA, (p2, "P2", null)); // the rep changes the pick after the approval

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        var approvedWeek = preview.Scheduled.Where(s => s.WeekStart == Week7Sep && s.ContactId == env.DoctorA).ToList();
        Assert.All(approvedWeek, s => Assert.True(s.IsFixed));
        Assert.Equal(p1, Assert.Single(Assert.Single(approvedWeek).ContentItems!).ProductId);
        var draftWeek = preview.Scheduled.Single(s => s.WeekStart == "2026-09-14" && s.ContactId == env.DoctorA);
        Assert.False(draftWeek.IsFixed);
        Assert.Equal(p2, Assert.Single(draftWeek.ContentItems!).ProductId);
        // The stored atom is untouched.
        Assert.Equal(frozen, atom.ContentItems.Select(i => (i.ProductId, i.Source, i.Order)).ToList());
    }

    // ── 8 · the pick is written through the existing selection update ────────────────────────────────────────────

    [Fact]
    public async Task The_pick_rides_on_the_selection_update_null_keeps_empty_clears_and_mdm_is_fail_closed()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var contact = Guid.NewGuid();
        var account = Guid.NewGuid();
        var store = new SessionList();
        var session = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ResourceId = "rep-1", Status = PlanningSessionStatus.Draft,
            Selection = new PlanningSessionSelection
            {
                SelectedContacts =
                {
                    new PlanningSessionSelectedContact
                    {
                        ContactId = contact, AccountId = account,
                        Products = { new PlanningSessionSelectedProduct { ProductId = p1, ProductCode = "P1" } }
                    }
                }
            }
        };
        store.Items.Add(session);
        var mdm = new FakeProducts { Known = { p1, p2 } };
        var handler = new UpdatePlanningSessionSelectionHandler(
            TenantOf(Tenant), new NullActorContext(), store, TestCallerScope.Unrestricted("rep-1"), mdm);

        Task<Diten.CrmService.Application.Common.Models.Response<bool>> Update(IReadOnlyList<SelectedProductInput>? products)
            => handler.Handle(new UpdatePlanningSessionSelectionCommand(
                session.Id, null, null, new[] { new SelectedContactInput(contact, account, null, products) },
                null, null, null, null, null), default);
        IReadOnlyList<Guid> Stored() => session.Selection.SelectedContacts.Single().Products.Select(p => p.ProductId).ToList();

        Assert.True((await Update(null)).IsSuccessful);          // null keeps
        Assert.Equal(new[] { p1 }, Stored());
        Assert.Empty(mdm.Asked);

        var set = await Update(new[] { new SelectedProductInput(p1, "P1", null), new SelectedProductInput(p2, "P2", "non-promo") });
        Assert.True(set.IsSuccessful);
        Assert.Equal(new[] { p1, p2 }, Stored());
        Assert.Equal("non-promo", session.Selection.SelectedContacts.Single().Products[1].Role);
        Assert.Equal(new[] { p2 }, mdm.Asked);                  // a stored product is not asked again

        var notFound = await Update(new[] { new SelectedProductInput(unknown, "X", null) });
        Assert.Equal((400, PlanningSessionProductPick.ProductNotFound), (notFound.StatusCode, notFound.Errors![0]));
        Assert.Equal(new[] { p1, p2 }, Stored());                // nothing changed

        mdm.Down = true;
        var down = await Update(new[] { new SelectedProductInput(Guid.NewGuid(), "Y", null) });
        Assert.Equal((503, PlanningSessionProductPick.ProductLookupUnavailable), (down.StatusCode, down.Errors![0]));
        mdm.Down = false;

        var badRole = await Update(new[] { new SelectedProductInput(p1, "P1", "featured") });
        Assert.Equal((400, PlanningSessionProductPick.InvalidProductRole), (badRole.StatusCode, badRole.Errors![0]));
        var tooMany = await Update(Enumerable.Range(0, 21).Select(_ => new SelectedProductInput(Guid.NewGuid(), null, null)).ToList());
        Assert.Equal((400, PlanningSessionProductPick.TooManyProducts), (tooMany.StatusCode, tooMany.Errors![0]));

        Assert.True((await Update(Array.Empty<SelectedProductInput>())).IsSuccessful); // [] clears
        Assert.Empty(Stored());
    }

    // ── 10 · the preview's product summaries ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_preview_counts_products_per_doctor_and_week_and_the_doctors_without_products()
    {
        var env = Env.WithTwoDoctors();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        Pick(env, env.DoctorA, (p1, "P1", null), (p2, "P2", StrategyProductLineRoles.NonPromo));

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        Assert.Equal(PortfolioStatuses.Undefined, preview.PortfolioStatus);
        Assert.Equal(1, preview.DoctorsWithoutProducts); // doctor B has no pick and no play
        Assert.Equal(
            new[] { (p1, "P1", 1), (p2, "P2", 1) }.OrderBy(x => x.Item2),
            preview.ProductDistribution!.Select(d => (d.ProductId, d.ProductCode!, d.DoctorCount)).OrderBy(x => x.Item2));

        var week = preview.WeekCapacity!.Single(w => w.ProductVisitCounts!.Count > 0);
        Assert.Equal(
            new[] { ("P1", 1, 1), ("P2", 1, 0) },
            week.ProductVisitCounts!.Select(c => (c.ProductCode!, c.Visits, c.PromoVisits)).OrderBy(x => x.Item1));

        var doctorA = preview.Content.Single(c => c.ContactId == env.DoctorA);
        Assert.Equal(new[] { "P1", "P2" }, doctorA.Products!.Select(p => p.ProductCode));
        Assert.All(doctorA.Products!, p => Assert.Equal(PlannedVisitContentItemSources.RepPick, p.Source));
        var slotB = preview.Scheduled.Single(s => s.ContactId == env.DoctorB);
        Assert.Contains(VisitContentSequenceReasonCodes.NoProducts, slotB.ProductWarnings!);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static void Pick(Env env, Guid doctor, params (Guid Id, string Code, string? Role)[] products)
        => env.Session.Selection.SelectedContacts.Single(c => c.ContactId == doctor).Products = products
            .Select(p => new PlanningSessionSelectedProduct { ProductId = p.Id, ProductCode = p.Code, Role = p.Role })
            .ToList();

    /// <summary>The MDM Global Product proof: known ids are valid, others not found; <see cref="Down"/> = unreachable.</summary>
    private sealed class FakeProducts : IStrategyTemplateProductReferenceValidator
    {
        public HashSet<Guid> Known { get; } = new();
        public bool Down { get; set; }
        public List<Guid> Asked { get; } = new();

        public Task<IStrategyTemplateProductReferenceValidator.Outcome> ValidateAsync(
            string referenceKind, Guid referenceId, CancellationToken cancellationToken)
        {
            Asked.Add(referenceId);
            return Task.FromResult(Down
                ? IStrategyTemplateProductReferenceValidator.Outcome.Unavailable
                : Known.Contains(referenceId)
                    ? IStrategyTemplateProductReferenceValidator.Outcome.Valid
                    : IStrategyTemplateProductReferenceValidator.Outcome.NotFound);
        }
    }
}
