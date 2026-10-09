using System.Text.RegularExpressions;
using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitScope;

/// <summary>
/// WP-VP-FIX-2 — (D9) a planning-session update leaves a target list it does not send untouched (null = unchanged,
/// [] = clear, the three lists independent; the session's other selection-bound state survives) on the PRODUCTION
/// handler and the API request mapping; (F-1) the shared Turkish-insensitive search pattern the account / contact
/// repositories now use: "Hamidiye" finds "HAMİDİYE", "şişli" finds "ŞİŞLİ", "şirin" finds "ŞİRİN", and a regex
/// character in the term is plain text.
/// </summary>
public sealed class VisitPlanningFix2Tests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string Me = "11111111-1111-1111-1111-111111111111";

    // ═════════════════════════════════ D9 · null = unchanged ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task An_update_that_sends_no_target_list_keeps_the_whole_selection()
    {
        var (session, store) = Seeded();
        var manualOrder = session.ManualVisitOrder.ToList();

        var r = await Handler(store).Handle(Update(session.Id, targetWeekStart: "2026-10-26"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(2, session.Selection.SelectedAccountIds.Count);
        Assert.Single(session.Selection.SelectedPharmacyIds);
        var doctor = Assert.Single(session.Selection.SelectedContacts);
        Assert.NotNull(doctor.AccountId);
        Assert.NotNull(doctor.AccountContactLinkId);
        Assert.Equal(manualOrder, session.ManualVisitOrder);     // selection-bound state survives too
        Assert.Equal("2026-10-26", session.TargetWeekStart);       // the field the Edit form does send still changes
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-0000000000aa"), session.Selection.SegmentId); // stored history kept
    }

    [Fact]
    public async Task Only_the_list_that_is_sent_changes()
    {
        var (session, store) = Seeded();
        var newAccount = Guid.NewGuid();

        await Handler(store).Handle(Update(session.Id, accounts: new List<Guid> { newAccount }), default);

        Assert.Equal(new[] { newAccount }, session.Selection.SelectedAccountIds);
        Assert.Single(session.Selection.SelectedPharmacyIds);   // untouched
        Assert.Single(session.Selection.SelectedContacts);      // untouched
    }

    [Fact]
    public async Task An_empty_list_is_an_explicit_clear()
    {
        var (session, store) = Seeded();

        await Handler(store).Handle(Update(session.Id, contacts: new List<SelectedContactInput>()), default);

        Assert.Empty(session.Selection.SelectedContacts);         // cleared
        Assert.Equal(2, session.Selection.SelectedAccountIds.Count); // others untouched
        Assert.Single(session.Selection.SelectedPharmacyIds);
    }

    [Fact]
    public void The_api_request_keeps_an_absent_doctor_list_null_and_an_empty_one_empty()
    {
        Assert.Null(new UpdatePlanningSessionRequest().ToContacts());
        Assert.Empty(new UpdatePlanningSessionRequest { SelectedContacts = new List<SelectedContactRequest>() }.ToContacts()!);
    }

    // ═════════════════════════════════ F-1 · Turkish-insensitive search ═══════════════════════════════════════════════

    [Theory]
    [InlineData("Hamidiye")]
    [InlineData("HAMİDİYE")]
    [InlineData("hamidiye")]
    [InlineData("HAMIDIYE")]
    public void Every_spelling_of_an_i_finds_the_same_account(string term)
        => Assert.True(Matches(term, "HAMİDİYE ECZANESİ"), term);

    [Theory]
    [InlineData("şişli")]
    [InlineData("ŞİŞLİ")]
    [InlineData("Şişli")]
    public void The_other_turkish_letters_fold_through_the_i_option(string term)
    {
        Assert.True(Matches(term, "ŞİŞLİ ETFAL HASTANESİ"), term);
        Assert.True(Matches(term, "şişli klinik"), term);
    }

    [Fact]
    public void A_regex_character_in_the_term_is_plain_text()
    {
        var pattern = TurkishInsensitivePattern.Build("a.b(");

        Assert.Equal(@"a\.b\(", pattern);
        Assert.True(Matches("a.b(", "x a.b( y"));
        Assert.False(Matches("a.b(", "x aXb( y")); // the dot is not a wildcard
    }

    [Fact]
    public void A_contact_search_for_sirin_finds_SIRIN()
    {
        Assert.True(Matches("şirin", "Dr. ŞİRİN YILMAZ"));
        Assert.True(Matches("Şirin", "Dr. ŞİRİN YILMAZ"));
        Assert.False(Matches("sirin", "Dr. ŞİRİN YILMAZ")); // s ≠ ş — only the i letters are folded
    }

    [Fact]
    public void The_account_and_contact_repositories_search_through_the_shared_pattern()
    {
        foreach (var repository in new[] { "AccountRepository.cs", "ContactRepository.cs" })
        {
            var source = File.ReadAllText(Path.Combine(CrmRoot(), "src", "Diten.CrmService.Persistence", "Repositories", repository));
            Assert.Contains("TurkishInsensitivePattern.Build(search!.Trim())", source);
            Assert.DoesNotContain("var term = search!.Trim();", source);
        }
    }

    // ═════════════════════════════════ helpers ═════════════════════════════════════════════════════════════════════════

    /// <summary>The pattern exactly as the repositories send it: the built pattern with the case-insensitive option.</summary>
    private static bool Matches(string term, string value)
        => Regex.IsMatch(value, TurkishInsensitivePattern.Build(term), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static (PlanningSession Session, Store Store) Seeded()
    {
        var session = new PlanningSession
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            CyclePeriodId = Guid.NewGuid(),
            ResourceId = Me,
            Status = PlanningSessionStatus.Draft,
            TargetWeekStart = "2026-10-19",
            ManualVisitOrder = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            Selection = new PlanningSessionSelection
            {
                SelectedAccountIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
                SelectedPharmacyIds = new List<Guid> { Guid.NewGuid() },
                SelectedContacts = new List<PlanningSessionSelectedContact>
                {
                    new() { ContactId = Guid.NewGuid(), AccountId = Guid.NewGuid(), AccountContactLinkId = Guid.NewGuid() }
                },
                SegmentId = Guid.Parse("00000000-0000-0000-0000-0000000000aa")
            }
        };
        return (session, new Store(session));
    }

    private static UpdatePlanningSessionSelectionHandler Handler(Store store)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Tenant);
        return new UpdatePlanningSessionSelectionHandler(tenant, new NullActorContext(), store, new TestCallerScope(Me));
    }

    private static UpdatePlanningSessionSelectionCommand Update(
        Guid id, List<Guid>? accounts = null, List<Guid>? pharmacies = null, List<SelectedContactInput>? contacts = null,
        string? targetWeekStart = null)
        => new(id, accounts, pharmacies, contacts, null, null, null, null, null, targetWeekStart);

    private static string CrmRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Diten.CrmService.Persistence"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("CRM root not found");
    }

    private sealed class Store(PlanningSession session) : IPlanningSessionRepository
    {
        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult<PlanningSession?>(tenantId == session.TenantId && id == session.Id ? session : null);
        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(new[] { session });
        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(new[] { session });
        public Task InsertAsync(PlanningSession entity, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }
}
