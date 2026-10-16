using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using SecurityClaim = System.Security.Claims.Claim;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W4 · W4-BE-c (Acceptance) on the PRODUCTION rules, recorder, reader, visibility and handlers with the stores
/// faked at their seams: the 15-cell opportunity matrix; the total rule (400 <c>potential_exceeds_total</c>); the 90-day
/// rule (89 / 90 / 91); the thresholds (built-in / tenant default / product / invalid 400 + history); the Concept Graph
/// rows (mapped / not mapped / custom type codes); the visibility (own / team / read-all / a stranger 404) and the
/// aggregate (&lt;5 hidden); the recorder's history rows with their source; tenant isolation.
/// </summary>
public sealed class VwW4DoctorInsightsTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000c4");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-0000-0000-0000000000c4");
    private static readonly Guid Product = Guid.Parse("a0000000-0000-0000-0000-0000000000aa");
    private static readonly Guid OtherProduct = Guid.Parse("b0000000-0000-0000-0000-0000000000bb");
    private const string Rep = "rep-1", Manager = "manager-1", Stranger = "stranger-1";

    // ═══ 1 · the opportunity matrix (§2.3) ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("high", "none", "opportunity")]
    [InlineData("high", "occasional", "opportunity")]
    [InlineData("high", "half", "grow")]
    [InlineData("high", "mostly", "keep")]
    [InlineData("high", "always", "keep")]
    [InlineData("medium", "none", "grow")]
    [InlineData("medium", "occasional", "grow")]
    [InlineData("medium", "half", "grow")]
    [InlineData("medium", "mostly", "keep")]
    [InlineData("medium", "always", "keep")]
    [InlineData("low", "none", "low_priority")]
    [InlineData("low", "occasional", "low_priority")]
    [InlineData("low", "half", "low_priority")]
    [InlineData("low", "mostly", "keep")]
    [InlineData("low", "always", "keep")]
    public void The_matrix_has_fifteen_cells(string potentialClass, string share, string expected)
        => Assert.Equal(expected, OpportunityRules.Classify(potentialClass, share));

    [Theory]
    [InlineData(null, "none")]
    [InlineData("high", null)]
    [InlineData("high", "lots")]
    [InlineData("huge", "none")]
    public void No_class_no_share_or_an_unknown_value_has_no_label(string? potentialClass, string? share)
        => Assert.Null(OpportunityRules.Classify(potentialClass, share));

    // ═══ 2 · the potential rules ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(9, "low")]
    [InlineData(10, "medium")]
    [InlineData(30, "medium")]
    [InlineData(31, "high")]
    [InlineData(0, "low")]
    public void The_class_follows_the_thresholds_low_below_10_high_above_30_by_default(int total, string expected)
        => Assert.Equal(expected, PotentialRules.Classify(total, PotentialRules.Resolve(Array.Empty<ProductPotentialThreshold>(), Product)));

    [Theory]
    [InlineData(89, false)]
    [InlineData(90, false)]
    [InlineData(91, true)]
    public void The_numbers_are_due_when_older_than_90_days(int daysAgo, bool due)
    {
        var now = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal(due, PotentialRules.IsDue(now.AddDays(-daysAgo), now));
        Assert.True(PotentialRules.IsDue(null, now)); // never given ⇒ asked
    }

    [Fact]
    public async Task The_indications_may_not_exceed_the_doctor_total_on_a_report_or_a_lower_manual_total()
    {
        var w = new World();
        var doctor = w.Doctor("Dr. Ayşe", w.NodeA);

        // 25 + 10 > 30 ⇒ 400, nothing written
        var refused = await w.Recorder().RecordFromReportAsync(Tenant, doctor, Guid.NewGuid(), Rep, null, 30,
            new[] { new DoctorProductInsightInput(Product, new[] { w.Row(w.Ind1, w.Prof1, 25), w.Row(w.Ind2, null, 10) }, null, "half") }, default);
        Assert.Equal((false, DoctorInsightsErrors.PotentialExceedsTotal, 400), (refused.Ok, refused.Code, refused.Status));
        Assert.Empty(w.Potentials.Items);
        Assert.Empty(w.Profiles.Items);

        // 20 + 10 = 30 ⇒ fine
        Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, doctor, Guid.NewGuid(), Rep, null, 30,
            new[] { new DoctorProductInsightInput(Product, new[] { w.Row(w.Ind1, w.Prof1, 20), w.Row(w.Ind2, null, 10) }, null, "half") }, default)).Ok);

        // a manual total under the stored product's 30 ⇒ 400
        var lower = await w.Commands(Rep, DoctorInsightsPermissions.Read, DoctorInsightsPermissions.Manage).Handle(
            new UpdateDoctorProfileCommand(doctor, new DoctorProfileInput(null, null, null, 29)), default);
        Assert.Equal(400, lower.StatusCode);
        Assert.Contains(DoctorInsightsErrors.PotentialExceedsTotal, lower.Errors!);
        Assert.Equal(30, w.Profiles.Items.Single().TotalMonthlyPatients);
    }

    [Fact]
    public async Task F5_the_recorder_writes_no_indication_numbers_for_a_product_without_indication_rows()
    {
        // WP-VW-W4 (W4-FIX-b, F-5): OtherProduct has no indication × profile rows in the Concept Graph, Product has.
        var w = new World();
        var doctor = w.Doctor("Dr. Ayşe", w.NodeA);
        Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, doctor, Guid.NewGuid(), Rep, null, 30, new[]
        {
            new DoctorProductInsightInput(OtherProduct, null, true, "half"),
            new DoctorProductInsightInput(Product, null, true, "half")
        }, default)).Ok);

        // the row-less product: "no patients" is NOT recorded (no false statement), its three months do not start; the share stays
        var other = w.Potentials.Items.Single(p => p.GlobalProductId == OtherProduct);
        Assert.False(other.NoPatientsInIndications);
        Assert.Null(other.AsOf);
        Assert.Equal("half", other.ShareBucketCode);
        Assert.DoesNotContain(w.PotentialHistory.Items, h => h.GlobalProductId == OtherProduct && h.Field == "noPatientsInIndications");
        // the product with rows keeps the old rule
        var own = w.Potentials.Items.Single(p => p.GlobalProductId == Product);
        Assert.True(own.NoPatientsInIndications);
        Assert.NotNull(own.AsOf);

        // rows sent for a row-less product are dropped, not refused (the check passes, nothing is written for them)
        Assert.True((await w.Recorder().CheckFromReportAsync(Tenant, doctor, null, 30,
            new[] { new DoctorProductInsightInput(OtherProduct, new[] { w.Row(w.Ind1, w.Prof1, 5) }, null, null) }, default)).Ok);
    }

    // ═══ 3 · the thresholds ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Thresholds_fall_back_product_then_tenant_default_then_10_30_and_an_invalid_pair_is_400()
    {
        var w = new World();
        var queries = w.Queries(Rep, DoctorInsightsPermissions.ThresholdsManage);
        var commands = w.Commands("marketing-1", DoctorInsightsPermissions.ThresholdsManage);

        var empty = (await queries.Handle(new GetPotentialThresholdsQuery(), default)).Data!;
        Assert.Equal((10, 30, ThresholdSources.BuiltIn), (empty.Default.LowBelow, empty.Default.HighAbove, empty.Default.Source));

        Assert.Equal(400, (await commands.Handle(new UpdatePotentialThresholdCommand(null, new PotentialThresholdInput(40, 20)), default)).StatusCode);
        Assert.Equal(400, (await commands.Handle(new UpdatePotentialThresholdCommand(null, new PotentialThresholdInput(0, 20)), default)).StatusCode);
        Assert.Equal(400, (await commands.Handle(new UpdatePotentialThresholdCommand(Product, new PotentialThresholdInput(5, null)), default)).StatusCode);

        Assert.True((await commands.Handle(new UpdatePotentialThresholdCommand(null, new PotentialThresholdInput(5, 20)), default)).IsSuccessful);
        Assert.Equal(new EffectiveThreshold(5, 20, ThresholdSources.TenantDefault), PotentialRules.Resolve(w.Thresholds.Items, Product));
        Assert.True((await commands.Handle(new UpdatePotentialThresholdCommand(Product, new PotentialThresholdInput(20, 20)), default)).IsSuccessful); // low = high is allowed
        Assert.Equal(new EffectiveThreshold(20, 20, ThresholdSources.Product), PotentialRules.Resolve(w.Thresholds.Items, Product));
        Assert.Equal(new EffectiveThreshold(5, 20, ThresholdSources.TenantDefault), PotentialRules.Resolve(w.Thresholds.Items, OtherProduct));

        // an update with an old version is 409; the history says who changed what
        Assert.Equal(409, (await commands.Handle(new UpdatePotentialThresholdCommand(Product, new PotentialThresholdInput(15, 25, ExpectedVersion: 7)), default)).StatusCode);
        Assert.True((await commands.Handle(new UpdatePotentialThresholdCommand(Product, new PotentialThresholdInput(15, 20, ExpectedVersion: 1)), default)).IsSuccessful);
        var history = (await queries.Handle(new GetPotentialThresholdHistoryQuery(), default)).Data!;
        Assert.Equal(5, history.Total); // default: low + high; product: low + high; product again: low only
        Assert.Contains(history.Items, h => h.ProductId == Product && h.Field == "lowBelow" && h.OldValue == "20" && h.NewValue == "15" && h.By == "marketing-1");

        var listed = (await queries.Handle(new GetPotentialThresholdsQuery(), default)).Data!;
        Assert.Equal((5, 20, ThresholdSources.TenantDefault), (listed.Default.LowBelow, listed.Default.HighAbove, listed.Default.Source));
        Assert.Equal((Product, 15, 20), (listed.Products.Single().ProductId!.Value, listed.Products.Single().LowBelow, listed.Products.Single().HighAbove));
    }

    // ═══ 4 · the Concept Graph rows ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_product_rows_come_from_the_concept_graph_and_a_missing_mapping_says_no_concept_graph()
    {
        var w = new World();
        var read = await w.Indications().ReadAsync(Tenant, Product, default);
        Assert.Null(read.Reason);
        Assert.Equal(new[] { ("Hipertansiyon", (string?)"Yaşlı hasta"), ("Hipertansiyon", "Diyabetik hasta"), ("Kalp yetmezliği", null) },
            read.Items.Select(i => (i.IndicationName, i.ProfileName)));

        // no product node for the other product; another tenant's graph is never read
        Assert.Equal(DoctorInsightsErrors.NoConceptGraph, (await w.Indications().ReadAsync(Tenant, OtherProduct, default)).Reason);
        Assert.Equal(DoctorInsightsErrors.NoConceptGraph, (await w.Indications().ReadAsync(OtherTenant, Product, default)).Reason);

        // type codes are configuration (tenant data): a tenant whose indication type is not CT-IND finds nothing …
        Assert.Equal(DoctorInsightsErrors.NoConceptGraph, (await w.Indications(new Settings("IND-X", "PROF-X")).ReadAsync(Tenant, Product, default)).Reason);
        // … and the product node without an indication answers no_indications
        w.Relationships.Items.RemoveAll(r => r.RelationshipType == ConceptRelationshipTypes.Addresses);
        Assert.Equal(DoctorInsightsErrors.NoIndications, (await w.Indications().ReadAsync(Tenant, Product, default)).Reason);
    }

    // ═══ 5 · visibility (§2.4) ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_rep_sees_own_doctors_a_manager_the_team_read_all_everyone_and_a_stranger_is_404()
    {
        var w = new World();
        var own = w.Doctor("Dr. Own", w.NodeA);         // rep's exact node
        var team = w.Doctor("Dr. Team", w.NodeB);       // under the manager's subtree, not the rep's
        var foreign = w.Doctor("Dr. Foreign", w.NodeC); // another region

        async Task<int> Status(string caller, Guid doctor, params string[] perms)
            => (await w.Queries(caller, perms).Handle(new GetDoctorProfileQuery(doctor), default)).StatusCode;

        Assert.Equal(200, await Status(Rep, own, DoctorInsightsPermissions.Read));
        Assert.Equal(404, await Status(Rep, team, DoctorInsightsPermissions.Read));
        Assert.Equal(404, await Status(Rep, foreign, DoctorInsightsPermissions.Read));
        Assert.Equal(200, await Status(Manager, own, DoctorInsightsPermissions.Read));
        Assert.Equal(200, await Status(Manager, team, DoctorInsightsPermissions.Read));
        Assert.Equal(404, await Status(Manager, foreign, DoctorInsightsPermissions.Read));
        Assert.Equal(404, await Status(Stranger, own, DoctorInsightsPermissions.Read));     // no assignment ⇒ nobody (never the whole tenant)
        Assert.Equal(200, await Status(Stranger, foreign, DoctorInsightsPermissions.ReadAll));
        Assert.Equal(404, await Status(Rep, Guid.NewGuid(), DoctorInsightsPermissions.Read)); // missing = foreign: same 404

        // the opportunity list: mine = the rep's exact nodes, team = the manager's subtree
        foreach (var d in new[] { own, team, foreign })
        {
            Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, d, Guid.NewGuid(), Rep, null, 100,
                new[] { new DoctorProductInsightInput(Product, new[] { w.Row(w.Ind1, w.Prof1, 50) }, null, "none") }, default)).Ok);
        }

        async Task<Guid[]> Opps(string caller, string scope, params string[] perms)
            => (await w.Queries(caller, perms).Handle(new ListDoctorOpportunitiesQuery(scope, null), default)).Data!.Items.Select(i => i.ContactId).OrderBy(x => x).ToArray();

        Assert.Equal(new[] { own }, await Opps(Rep, "mine", DoctorInsightsPermissions.Read));
        Assert.Empty(await Opps(Rep, "team", DoctorInsightsPermissions.Read));
        Assert.Equal(new[] { own, team }.OrderBy(x => x).ToArray(), await Opps(Manager, "team", DoctorInsightsPermissions.Read));
        Assert.Equal(new[] { own, team, foreign }.OrderBy(x => x).ToArray(), await Opps(Stranger, "mine", DoctorInsightsPermissions.ReadAll));
        var row = (await w.Queries(Rep, DoctorInsightsPermissions.Read).Handle(new ListDoctorOpportunitiesQuery("mine", Product), default)).Data!.Items.Single();
        Assert.Equal(("Dr. Own", "high", "none", "opportunity"), (row.DisplayName, row.PotentialClass, row.ShareBucketCode, row.OpportunityCode));
        Assert.Equal(400, (await w.Queries(Rep, DoctorInsightsPermissions.Read).Handle(new ListDoctorOpportunitiesQuery("all", null), default)).StatusCode);
    }

    [Fact]
    public async Task The_marketing_aggregate_names_nobody_and_hides_cells_under_five()
    {
        var w = new World();
        for (var i = 0; i < 9; i++)
        {
            var d = w.Doctor("Dr. " + i, w.NodeA);
            Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, d, Guid.NewGuid(), Rep, null, 100,
                new[] { new DoctorProductInsightInput(Product, new[] { w.Row(w.Ind1, w.Prof1, i < 5 ? 50 : 5) }, null, i < 5 ? "none" : "mostly") }, default)).Ok);
        }

        var agg = (await w.Queries("marketing-1", DoctorInsightsPermissions.ReadAggregate).Handle(new GetDoctorInsightAggregatesQuery(Product), default)).Data!;
        Assert.Equal("9", agg.DoctorCount);
        Assert.Equal("5", agg.Cells.Single(c => c.PotentialClass == "high" && c.ShareBucketCode == "none").Count);
        Assert.Equal("<5", agg.Cells.Single(c => c.PotentialClass == "low" && c.ShareBucketCode == "mostly").Count);
        Assert.Equal("0", agg.Cells.Single(c => c.PotentialClass == "medium" && c.ShareBucketCode == "half").Count);
        Assert.Equal(("5", "<5"), (agg.Opportunities.Single(o => o.OpportunityCode == "opportunity").Count, agg.Opportunities.Single(o => o.OpportunityCode == "keep").Count));
        Assert.DoesNotContain("Dr.", System.Text.Json.JsonSerializer.Serialize(agg));

        Assert.Equal("0", (await w.Queries("marketing-1", DoctorInsightsPermissions.ReadAggregate).Handle(new GetDoctorInsightAggregatesQuery(OtherProduct), default)).Data!.DoctorCount);
    }

    [Fact]
    public void The_endpoints_ask_for_the_right_keys()
    {
        static ClaimsPrincipal User(params string[] keys) => new(new ClaimsIdentity(keys.Select(k => new SecurityClaim("permission", k)), "test"));
        Assert.True(DoctorInsightsController.MayRead(User(DoctorInsightsPermissions.Read)));
        Assert.True(DoctorInsightsController.MayRead(User(DoctorInsightsPermissions.ReadAll)));
        Assert.False(DoctorInsightsController.MayRead(User(DoctorInsightsPermissions.ReadAggregate, DoctorInsightsPermissions.ThresholdsManage)));
        Assert.True(DoctorInsightsController.MayReadThresholds(User(DoctorInsightsPermissions.ThresholdsManage)));
        Assert.True(DoctorInsightsController.MayReadAggregate(User(DoctorInsightsPermissions.ReadAggregate)));
        Assert.False(DoctorInsightsController.MayReadAggregate(User(DoctorInsightsPermissions.Read)));
        Assert.Equal((int?)3, DoctorInsightsController.IfMatch("W/\"3\""));
        Assert.Equal((int?)4, DoctorInsightsController.IfMatch("\"4\""));
        Assert.Null(DoctorInsightsController.IfMatch("abc"));
    }

    // ═══ 6 · the recorder (BE-a seam) ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_recorder_writes_profile_and_potential_with_report_sourced_history_and_restarts_the_three_months()
    {
        var w = new World();
        var doctor = w.Doctor("Dr. Ayşe", w.NodeA);
        var report = Guid.NewGuid();

        Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, doctor, report, Rep,
            new DoctorProfileSuggestion("early_adopter", "analytical", "local"), 80,
            new[] { new DoctorProductInsightInput(Product, new[] { w.Row(w.Ind1, w.Prof1, 40), w.Row(w.Ind2, null, 5) }, null, "occasional") }, default)).Ok);

        var source = DoctorInsightsSources.Report(report);
        Assert.Equal(new[] { "adoptionTypologyCode", "communicationStyleCode", "kolLevelCode", "totalMonthlyPatients" },
            w.ProfileHistory.Items.Select(h => h.Field).OrderBy(f => f, StringComparer.Ordinal));
        Assert.All(w.ProfileHistory.Items, h => Assert.Equal((source, Rep, (string?)null), (h.Source, h.By, h.OldValue)));
        Assert.Equal(3, w.PotentialHistory.Items.Count(h => h.Source == source && h.Field.StartsWith("row:", StringComparison.Ordinal) || h.Field == "shareBucketCode"));

        var read = (await w.Queries(Rep, DoctorInsightsPermissions.Read).Handle(new GetDoctorPotentialsQuery(doctor, new[] { Product, OtherProduct }), default)).Data!;
        var mine = read.Items.Single(i => i.ProductId == Product);
        Assert.Equal((45, "high", "opportunity", false), (mine.IndicationTotal, mine.PotentialClass, mine.OpportunityCode, mine.IsDue));
        var never = read.Items.Single(i => i.ProductId == OtherProduct);
        Assert.Equal((true, (string?)null, 0), (never.IsDue, never.PotentialClass, never.Version)); // never asked ⇒ due, no class
        Assert.Equal((80, false), (read.TotalMonthlyPatients, read.TotalIsDue));

        // a later report that only updates the share keeps the numbers (and their AsOf); the old value is in the history
        var asOf = w.Potentials.Items.Single().AsOf;
        w.Clock.Advance(TimeSpan.FromDays(10));
        Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, doctor, Guid.NewGuid(), Rep, null, null,
            new[] { new DoctorProductInsightInput(Product, null, null, "mostly") }, default)).Ok);
        Assert.Equal((asOf, 45, "mostly"), (w.Potentials.Items.Single().AsOf, w.Potentials.Items.Single().IndicationTotal(), w.Potentials.Items.Single().ShareBucketCode));
        Assert.Contains(w.PotentialHistory.Items, h => h.Field == "shareBucketCode" && h.OldValue == "occasional" && h.NewValue == "mostly");

        // "no patients in these indications" = 0 ⇒ low; rows with it are refused
        Assert.True((await w.Recorder().RecordFromReportAsync(Tenant, doctor, Guid.NewGuid(), Rep, null, null,
            new[] { new DoctorProductInsightInput(Product, Array.Empty<DoctorPotentialRowInput>(), true, null) }, default)).Ok);
        Assert.Equal((0, true), (w.Potentials.Items.Single().IndicationTotal(), w.Potentials.Items.Single().NoPatientsInIndications));
        Assert.Equal(400, (await w.Recorder().CheckFromReportAsync(Tenant, doctor, null, null,
            new[] { new DoctorProductInsightInput(Product, new[] { w.Row(w.Ind1, w.Prof1, 1) }, true, null) }, default)).Status);
    }

    [Fact]
    public async Task The_recorder_checks_codes_products_and_rows_of_the_graph_before_writing()
    {
        var w = new World();
        var doctor = w.Doctor("Dr. Ayşe", w.NodeA);

        var badCode = await w.Recorder().CheckFromReportAsync(Tenant, doctor, new DoctorProfileSuggestion("visionary", null, null), null,
            Array.Empty<DoctorProductInsightInput>(), default);
        Assert.Equal((DoctorInsightsErrors.CodeInvalid, 400), (badCode.Code, badCode.Status));

        var badShare = await w.Recorder().CheckFromReportAsync(Tenant, doctor, null, null,
            new[] { new DoctorProductInsightInput(Product, null, null, "some") }, default);
        Assert.Equal(DoctorInsightsErrors.CodeInvalid, badShare.Code);

        var foreignRow = await w.Recorder().CheckFromReportAsync(Tenant, doctor, null, 50,
            new[] { new DoctorProductInsightInput(Product, new[] { w.Row(Guid.NewGuid(), null, 5) }, null, null) }, default);
        Assert.Equal(DoctorInsightsErrors.Invalid, foreignRow.Code);

        w.ProductCheck.Outcome = IStrategyTemplateProductReferenceValidator.Outcome.NotFound;
        Assert.Equal(DoctorInsightsErrors.ProductInvalid, (await w.Recorder().CheckFromReportAsync(Tenant, doctor, null, null,
            new[] { new DoctorProductInsightInput(Product, null, null, "half") }, default)).Code);
        w.ProductCheck.Outcome = IStrategyTemplateProductReferenceValidator.Outcome.Unavailable;
        Assert.Equal(503, (await w.Recorder().CheckFromReportAsync(Tenant, doctor, null, null,
            new[] { new DoctorProductInsightInput(Product, null, null, "half") }, default)).Status);

        Assert.Empty(w.Profiles.Items);
        Assert.Empty(w.Potentials.Items);
    }

    [Fact]
    public async Task A_manual_profile_edit_needs_the_current_version_and_reads_availability_and_preferences_from_their_owners()
    {
        var w = new World();
        var doctor = w.Doctor("Dr. Ayşe", w.NodeA);
        var commands = w.Commands(Rep, DoctorInsightsPermissions.Read, DoctorInsightsPermissions.Manage);

        var first = await commands.Handle(new UpdateDoctorProfileCommand(doctor, new DoctorProfileInput("innovator", "driver", "national", 60, 0)), default);
        Assert.True(first.IsSuccessful);
        Assert.Equal((1, "manual"), (first.Data!.Profile.Version, first.Data.History.First().Source));
        Assert.Equal(409, (await commands.Handle(new UpdateDoctorProfileCommand(doctor, new DoctorProfileInput("laggard", null, null, null, 0)), default)).StatusCode);

        w.Availability.Items.Add(new ContactAvailability
        {
            TenantId = Tenant, ContactId = doctor, Weekday = "tuesday", StartTime = "09:00", EndTime = "11:00", AvailabilityType = "visit",
            Preference = new VisitPreference { PreferredContactMethod = "visit" }
        });
        w.Preferences.Items.Add(new PreferenceRecord
        {
            TenantId = Tenant, SubjectType = ConsentSubjectType.Contact, SubjectId = doctor, Channel = "all",
            PreferenceType = PreferenceType.PreferredChannel, PreferenceValue = "email", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });
        w.Preferences.Items.Add(new PreferenceRecord
        {
            TenantId = Tenant, SubjectType = ConsentSubjectType.Contact, SubjectId = doctor, Channel = "all",
            PreferenceType = PreferenceType.DoNotContact, PreferenceValue = "x", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var read = (await w.Queries(Rep, DoctorInsightsPermissions.Read).Handle(new GetDoctorProfileQuery(doctor), default)).Data!;
        Assert.Equal(("innovator", "driver", "national", 60), (read.Profile.AdoptionTypologyCode, read.Profile.CommunicationStyleCode, read.Profile.KolLevelCode, read.Profile.TotalMonthlyPatients));
        Assert.Equal("tuesday", read.Availability.Single().Weekday);
        Assert.Equal(("preferred-channel", "email"), (read.Preferences.Single().PreferenceType, read.Preferences.Single().PreferenceValue));
        Assert.Single(w.Availability.Items); // read, never copied
    }

    // ═══ 7 · tenant isolation ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Another_tenant_s_doctor_profile_potential_and_thresholds_are_never_read()
    {
        var w = new World();
        var doctor = w.Doctor("Dr. Ayşe", w.NodeA);
        w.Profiles.Items.Add(new DoctorProfile { TenantId = OtherTenant, ContactId = doctor, KolLevelCode = "international", TotalMonthlyPatients = 5, Version = 1 });
        w.Potentials.Items.Add(new DoctorProductPotential { TenantId = OtherTenant, ContactId = doctor, GlobalProductId = Product, ShareBucketCode = "always", Version = 1 });
        w.Thresholds.Items.Add(new ProductPotentialThreshold { TenantId = OtherTenant, GlobalProductId = Product, LowBelow = 1, HighAbove = 2, Version = 1 });

        var profile = (await w.Queries(Rep, DoctorInsightsPermissions.Read).Handle(new GetDoctorProfileQuery(doctor), default)).Data!;
        Assert.Equal((null, null, 0), (profile.Profile.KolLevelCode, profile.Profile.TotalMonthlyPatients, profile.Profile.Version));
        var potentials = (await w.Queries(Rep, DoctorInsightsPermissions.Read).Handle(new GetDoctorPotentialsQuery(doctor, new[] { Product }), default)).Data!;
        Assert.Equal(((string?)null, ThresholdSources.BuiltIn), (potentials.Items.Single().ShareBucketCode, potentials.Items.Single().Threshold.Source));

        // the other tenant's caller never sees this tenant's doctor
        var other = new World(OtherTenant);
        Assert.Equal(404, (await other.Queries(Stranger, DoctorInsightsPermissions.ReadAll).Handle(new GetDoctorProfileQuery(doctor), default)).StatusCode);
    }

    // ═══ 8 · persistence: the GUID trap ═════════════════════════════════════════════════════════════════

    [Fact]
    public void Every_new_aggregate_is_class_mapped_and_stores_its_guids_as_strings()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        foreach (var type in new[] { typeof(DoctorProfile), typeof(DoctorProfileChange), typeof(DoctorProductPotential), typeof(DoctorPotentialRow),
                     typeof(DoctorProductPotentialChange), typeof(ProductPotentialThreshold), typeof(ProductPotentialThresholdChange) })
        {
            Assert.True(MongoDB.Bson.Serialization.BsonClassMap.IsClassMapRegistered(type), type.Name);
        }

        var potential = new DoctorProductPotential
        {
            TenantId = Tenant, ContactId = Guid.NewGuid(), GlobalProductId = Product,
            IndicationRows = { new DoctorPotentialRow { IndicationNodeId = Guid.NewGuid(), ProfileNodeId = Guid.NewGuid(), MonthlyPatients = 4 } }
        };
        var doc = MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(potential);
        Assert.Equal(MongoDB.Bson.BsonType.String, doc["ContactId"].BsonType);
        Assert.Equal(Product.ToString(), doc["GlobalProductId"].AsString);
        Assert.Equal(MongoDB.Bson.BsonType.String, doc["IndicationRows"].AsBsonArray[0]["IndicationNodeId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, doc["IndicationRows"].AsBsonArray[0]["ProfileNodeId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(new DoctorProfile { ContactId = Guid.NewGuid() })["ContactId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(new ProductPotentialThreshold { GlobalProductId = Product })["GlobalProductId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.Null, MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(new ProductPotentialThreshold())["GlobalProductId"].BsonType);
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private sealed class World
    {
        public readonly Guid TenantId;
        public readonly TestClock Clock = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));
        public readonly Guid Model = Guid.NewGuid(), Root = Guid.NewGuid(), NodeA = Guid.NewGuid(), NodeB = Guid.NewGuid(), NodeC = Guid.NewGuid();
        public readonly Guid Ind1 = Guid.NewGuid(), Ind2 = Guid.NewGuid(), Prof1 = Guid.NewGuid(), Prof2 = Guid.NewGuid();

        public FakeTerritoryModelRepo Models { get; } = new();
        public FakeTerritoryNodeRepo Nodes { get; } = new();
        public FakeTerritoryResourceAssignmentRepo Assignments { get; } = new();
        public FakeAccountTerritoryAssignmentRepo AccountAssignments { get; } = new();
        public InMemoryAccountContactLinks Links { get; } = new();
        public InMemoryContacts Contacts { get; } = new();
        public Store<DoctorProfile> Profiles { get; } = new();
        public Store<DoctorProfileChange> ProfileHistory { get; } = new();
        public Store<DoctorProductPotential> Potentials { get; } = new();
        public Store<DoctorProductPotentialChange> PotentialHistory { get; } = new();
        public Store<ProductPotentialThreshold> Thresholds { get; } = new();
        public Store<ProductPotentialThresholdChange> ThresholdHistory { get; } = new();
        public ConceptTypes Types { get; } = new();
        public ConceptNodes ConceptNodeStore { get; } = new();
        public ConceptRelationships Relationships { get; } = new();
        public Availabilities Availability { get; } = new();
        public PreferenceStore Preferences { get; } = new();
        public ProductCheck ProductCheck { get; } = new();

        public World(Guid? tenant = null)
        {
            TenantId = tenant ?? Tenant;
            var past = Clock.GetUtcNow().AddYears(-1);
            Models.Items.Add(new TerritoryModel { Id = Model, TenantId = TenantId, Status = "active", EffectiveFrom = past });
            TerritoryNode Node(Guid id, Guid? parent) => new() { Id = id, TenantId = TenantId, ModelId = Model, ParentTerritoryId = parent, Status = "active", EffectiveFrom = past, Name = id.ToString("N")[..4] };
            Nodes.Items.AddRange(new[] { Node(Root, null), Node(NodeA, Root), Node(NodeB, Root), Node(NodeC, null) });
            Assignments.Items.Add(Assign(Rep, NodeA, "exact-territory", past));
            Assignments.Items.Add(Assign(Manager, Root, "territory-subtree", past));

            // the concept graph: product –addresses→ Hipertansiyon (→ Yaşlı, Diyabetik), Kalp yetmezliği (no profile)
            Guid indType = Guid.NewGuid(), profType = Guid.NewGuid(), prodType = Guid.NewGuid(), productNode = Guid.NewGuid();
            Types.Items.AddRange(new[] { Type(indType, "CT-IND"), Type(profType, "CT-PROF"), Type(prodType, "CT-PROD") });
            ConceptNodeStore.Items.AddRange(new[]
            {
                ConceptNodeOf(productNode, prodType, "Ürün", ConceptExternalRefTypes.GlobalProduct, Product.ToString("D")),
                ConceptNodeOf(Ind1, indType, "Hipertansiyon"), ConceptNodeOf(Ind2, indType, "Kalp yetmezliği"),
                ConceptNodeOf(Prof1, profType, "Yaşlı hasta"), ConceptNodeOf(Prof2, profType, "Diyabetik hasta")
            });
            var archived = ConceptNodeOf(Guid.NewGuid(), indType, "Eski endikasyon");
            archived.ArchivedAt = past;
            ConceptNodeStore.Items.Add(archived);
            Relationships.Items.AddRange(new[]
            {
                Rel(productNode, Ind1, ConceptRelationshipTypes.Addresses, 1), Rel(productNode, Ind2, ConceptRelationshipTypes.Addresses, 2),
                Rel(productNode, archived.Id, ConceptRelationshipTypes.Addresses, 3),
                Rel(Ind1, Prof1, ConceptRelationshipTypes.LeadsTo, 1), Rel(Ind1, Prof2, ConceptRelationshipTypes.LeadsTo, 2)
            });
            // another tenant's identical product node is never read
            var foreignNode = ConceptNodeOf(Guid.NewGuid(), indType, "Yabancı", ConceptExternalRefTypes.GlobalProduct, Product.ToString("D"));
            foreignNode.TenantId = TenantId == Tenant ? OtherTenant : Tenant;
            ConceptNodeStore.Items.Add(foreignNode);
        }

        private TerritoryResourceAssignment Assign(string resource, Guid node, string scope, DateTimeOffset from) => new()
        {
            TenantId = TenantId, ModelId = Model, TerritoryId = node, Resource = new TerritoryResourceRef { ResourceId = resource, DisplayName = resource },
            CoverageScope = scope, Status = "active", IsPrimary = true, ValidFrom = from
        };

        private ConceptType Type(Guid id, string code) => new() { Id = id, TenantId = TenantId, ConceptTypeCode = code, ConceptTypeName = code, Status = ConceptStatuses.Active };

        private ConceptNode ConceptNodeOf(Guid id, Guid type, string name, string? refType = null, string? refId = null) => new()
        {
            Id = id, TenantId = TenantId, ConceptTypeId = type, ConceptNodeName = name, ConceptNodeCode = name, Status = ConceptStatuses.Active,
            EffectiveFrom = Clock.GetUtcNow().AddYears(-1), ExternalRefType = refType, ExternalRefId = refId
        };

        private ConceptRelationship Rel(Guid from, Guid to, string type, int priority) => new()
        {
            TenantId = TenantId, FromConceptNodeId = from, ToConceptNodeId = to, RelationshipType = type, Priority = priority,
            Status = ConceptStatuses.Active, EffectiveFrom = Clock.GetUtcNow().AddYears(-1)
        };

        /// <summary>A doctor linked to an account currently covered on <paramref name="node"/>.</summary>
        public Guid Doctor(string name, Guid node)
        {
            var contact = new Contact { TenantId = TenantId, DisplayName = name, Status = "active" };
            var account = Guid.NewGuid();
            Contacts.Items.Add(contact);
            Links.Items.Add(new AccountContactLink { TenantId = TenantId, AccountId = account, ContactId = contact.Id, Status = "active" });
            AccountAssignments.Items.Add(new AccountTerritoryAssignment
            {
                TenantId = TenantId, AccountId = account, TerritoryModelId = Model, TerritoryNodeId = node, AssignmentStatus = "active",
                EffectiveFrom = Clock.GetUtcNow().AddYears(-1)
            });
            return contact.Id;
        }

        public DoctorPotentialRowInput Row(Guid indication, Guid? profile, int patients) => new(indication, profile, patients);

        private Codes CodeCheck { get; } = new();

        public ProductIndicationProfileReader Indications(IDoctorInsightsSettings? settings = null)
            => new(Types, ConceptNodeStore, Relationships, settings, Clock);

        public DoctorInsightsRecorder Recorder()
            => new(Profiles, ProfileHistory, Potentials, PotentialHistory, CodeCheck, Indications(), ProductCheck, Clock);

        private DoctorInsightsReader Reader(string caller, string[] perms)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantId);
            var visibility = new DoctorInsightsVisibility(new TestCallerScope(caller, perms), new TeamScopeResolver(Assignments, Models, Nodes),
                AccountAssignments, Models, Links, Clock);
            return new DoctorInsightsReader(ctx, visibility, Contacts, Profiles, ProfileHistory, Potentials, PotentialHistory, Thresholds,
                Availability, Preferences, null, Clock);
        }

        public DoctorInsightsQueryHandlers Queries(string caller, params string[] perms)
            => new(Reader(caller, perms), Indications(), ThresholdHistory);

        public DoctorInsightsCommandHandlers Commands(string caller, params string[] perms)
            => new(Reader(caller, perms), Recorder(), new Actor(caller), ThresholdHistory, ProductCheck);
    }

    private sealed class Settings(string indication, string profile) : IDoctorInsightsSettings
    {
        public string IndicationConceptTypeCode { get; } = indication;
        public string ProfileConceptTypeCode { get; } = profile;
    }

    private sealed class Actor(string? name) : IActorContext
    {
        public string? ActorName { get; } = name;
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>The five BRD sets (valid codes as in the seed).</summary>
    private sealed class Codes : IReferenceDataValidator
    {
        private static readonly Dictionary<string, string[]> Sets = new()
        {
            [DoctorInsightsReferenceSets.ShareBucket] = ShareBuckets.All.ToArray(),
            [DoctorInsightsReferenceSets.AdoptionTypology] = ["innovator", "early_adopter", "early_majority", "late_majority", "laggard"],
            [DoctorInsightsReferenceSets.CommunicationStyle] = ["analytical", "driver", "amiable", "expressive"],
            [DoctorInsightsReferenceSets.KolLevel] = ["none", "local", "regional", "national", "international"]
        };

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(
                !Sets.TryGetValue(setCode, out var values) ? ReferenceValidationStatus.SetMissing
                : values.Contains(value) ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue, setCode, value));
    }

    private sealed class ProductCheck : IStrategyTemplateProductReferenceValidator
    {
        public IStrategyTemplateProductReferenceValidator.Outcome Outcome { get; set; } = IStrategyTemplateProductReferenceValidator.Outcome.Valid;

        public Task<IStrategyTemplateProductReferenceValidator.Outcome> ValidateAsync(string referenceKind, Guid referenceId, CancellationToken cancellationToken)
            => Task.FromResult(Outcome);
    }

    /// <summary>One in-memory store for every doctor insights repository (tenant-filtered like the Mongo ones).</summary>
    private sealed class Store<T> :
        IDoctorProfileRepository, IDoctorProfileChangeRepository, IDoctorProductPotentialRepository,
        IDoctorProductPotentialChangeRepository, IProductPotentialThresholdRepository, IProductPotentialThresholdChangeRepository
        where T : EntityBase
    {
        public List<T> Items { get; } = new();

        private IEnumerable<TX> Of<TX>(Guid tenantId) where TX : EntityBase => Items.OfType<TX>().Where(x => x.TenantId == tenantId && !x.IsDeleted);

        private Task Insert(EntityBase e) { Items.Add((T)e); return Task.CompletedTask; }

        private static Task<bool> Replace(EntityBase e, int expected)
        {
            if (e.Version != expected) return Task.FromResult(false);
            e.Version = expected + 1;
            return Task.FromResult(true);
        }

        public Task<DoctorProfile?> GetByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
            => Task.FromResult(Of<DoctorProfile>(tenantId).FirstOrDefault(p => p.ContactId == contactId));
        public Task<IReadOnlyList<DoctorProfile>> ListByContactsAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DoctorProfile>>(Of<DoctorProfile>(tenantId).Where(p => contactIds.Contains(p.ContactId)).ToList());
        public Task InsertAsync(DoctorProfile entity, CancellationToken cancellationToken) => Insert(entity);
        public Task<bool> ReplaceAsync(DoctorProfile entity, int expectedVersion, CancellationToken cancellationToken) => Replace(entity, expectedVersion);

        public Task InsertManyAsync(IReadOnlyCollection<DoctorProfileChange> rows, CancellationToken cancellationToken) { Items.AddRange(rows.Cast<T>()); return Task.CompletedTask; }
        public Task<IReadOnlyList<DoctorProfileChange>> ListByContactAsync(Guid tenantId, Guid contactId, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DoctorProfileChange>>(Of<DoctorProfileChange>(tenantId).Where(c => c.ContactId == contactId).OrderByDescending(c => c.At).Take(take).ToList());

        public Task<DoctorProductPotential?> GetAsync(Guid tenantId, Guid contactId, Guid productId, CancellationToken cancellationToken)
            => Task.FromResult(Of<DoctorProductPotential>(tenantId).FirstOrDefault(p => p.ContactId == contactId && p.GlobalProductId == productId));
        Task<IReadOnlyList<DoctorProductPotential>> IDoctorProductPotentialRepository.ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DoctorProductPotential>>(Of<DoctorProductPotential>(tenantId).Where(p => p.ContactId == contactId).ToList());
        public Task<IReadOnlyList<DoctorProductPotential>> ListByContactsAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, Guid? productId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DoctorProductPotential>>(Of<DoctorProductPotential>(tenantId)
                .Where(p => contactIds.Contains(p.ContactId) && (productId is null || p.GlobalProductId == productId)).ToList());
        public Task<IReadOnlyList<DoctorProductPotential>> ListAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DoctorProductPotential>>(Of<DoctorProductPotential>(tenantId).Where(p => productId is null || p.GlobalProductId == productId).ToList());
        public Task InsertAsync(DoctorProductPotential entity, CancellationToken cancellationToken) => Insert(entity);
        public Task<bool> ReplaceAsync(DoctorProductPotential entity, int expectedVersion, CancellationToken cancellationToken) => Replace(entity, expectedVersion);

        public Task InsertManyAsync(IReadOnlyCollection<DoctorProductPotentialChange> rows, CancellationToken cancellationToken) { Items.AddRange(rows.Cast<T>()); return Task.CompletedTask; }
        Task<IReadOnlyList<DoctorProductPotentialChange>> IDoctorProductPotentialChangeRepository.ListByContactAsync(Guid tenantId, Guid contactId, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DoctorProductPotentialChange>>(Of<DoctorProductPotentialChange>(tenantId).Where(c => c.ContactId == contactId).OrderByDescending(c => c.At).Take(take).ToList());

        Task<IReadOnlyList<ProductPotentialThreshold>> IProductPotentialThresholdRepository.ListAsync(Guid tenantId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProductPotentialThreshold>>(Of<ProductPotentialThreshold>(tenantId).ToList());
        public Task<ProductPotentialThreshold?> GetAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken)
            => Task.FromResult(Of<ProductPotentialThreshold>(tenantId).FirstOrDefault(t => t.GlobalProductId == productId));
        public Task InsertAsync(ProductPotentialThreshold entity, CancellationToken cancellationToken) => Insert(entity);
        public Task<bool> ReplaceAsync(ProductPotentialThreshold entity, int expectedVersion, CancellationToken cancellationToken)
        {
            var stored = Of<ProductPotentialThreshold>(entity.TenantId).Any(t => t.Id == entity.Id);
            entity.Version = expectedVersion + 1;
            return Task.FromResult(stored);
        }

        public Task InsertManyAsync(IReadOnlyCollection<ProductPotentialThresholdChange> rows, CancellationToken cancellationToken) { Items.AddRange(rows.Cast<T>()); return Task.CompletedTask; }
        Task<(IReadOnlyList<ProductPotentialThresholdChange> Items, long Total)> IProductPotentialThresholdChangeRepository.ListAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken)
        {
            var rows = Of<ProductPotentialThresholdChange>(tenantId).OrderByDescending(c => c.At).ToList();
            return Task.FromResult<(IReadOnlyList<ProductPotentialThresholdChange>, long)>((rows.Skip(skip).Take(take).ToList(), rows.Count));
        }
    }

    private sealed class ConceptTypes : IConceptTypeRepository
    {
        public List<ConceptType> Items { get; } = new();
        public Task<ConceptType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id));
        public Task<IReadOnlyList<ConceptType>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConceptType>>(Items.Where(x => x.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConceptType?> GetActiveByCodeAsync(Guid tenantId, Guid subjectId, string conceptTypeCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ConceptType entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ConceptType entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ConceptNodes : IConceptNodeRepository
    {
        public List<ConceptNode> Items { get; } = new();
        public Task<ConceptNode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id));
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConceptNode>>(Items.Where(x => x.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid tenantId, Guid subjectId, Guid conceptTypeId, string conceptNodeCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ConceptNode entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ConceptNode entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ConceptRelationships : IConceptRelationshipRepository
    {
        public List<ConceptRelationship> Items { get; } = new();
        public Task<ConceptRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id));
        public Task<IReadOnlyList<ConceptRelationship>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConceptRelationship>>(Items.Where(x => x.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<ConceptRelationship>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ConceptRelationship entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ConceptRelationship entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Availabilities : IContactAvailabilityRepository
    {
        public List<ContactAvailability> Items { get; } = new();
        public Task<ContactAvailability?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContactAvailability>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContactAvailability>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ContactAvailability>>(Items.Where(x => x.TenantId == tenantId && x.ContactId == contactId).ToList());
        public Task<IReadOnlyList<ContactAvailability>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ContactAvailability availability, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ContactAvailability availability, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PreferenceStore : IPreferenceRecordRepository
    {
        public List<PreferenceRecord> Items { get; } = new();
        public Task<PreferenceRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PreferenceRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PreferenceRecord>> ListForEvaluationAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PreferenceRecord>>(Items.Where(x => x.TenantId == tenantId && x.SubjectType == subjectType && x.SubjectId == subjectId).ToList());
        public Task<PreferenceRecord?> FindByExternalReferenceAsync(Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(PreferenceRecord record, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(PreferenceRecord record, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
