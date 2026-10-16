using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// CT (W4-BE-a K13, 2026-10-13) — every required answer of the report v2, one at a time. The BE-a acceptance asked for "a
/// missing entry per required field"; its test named three product fields, so removing e.g. the share-bucket requirement went
/// unnoticed (CT sabotage: 18/18 green). Here a COMPLETE report is valid, and blanking exactly one answer yields exactly that
/// one missing entry — for the doctor report (product section, doctor level, general, safety) and the pharmacy report.
/// </summary>
public sealed class VwW4ReportV2CompletenessTests
{
    private static readonly Guid ProductA = Guid.Parse("a1a1a1a1-0000-0000-0000-00000000000a");
    private static readonly Guid Gsku = Guid.Parse("b2b2b2b2-0000-0000-0000-00000000000b");

    /// <summary>Everything asked: a planned product with a competitor list, its potential and the doctor total due.</summary>
    private static readonly VisitReportV2CheckContext DoctorContext = new(
        VisitReportKinds.Doctor,
        new HashSet<Guid> { ProductA },
        new HashSet<Guid> { ProductA },
        TotalDue: true,
        new HashSet<Guid> { ProductA },
        PharmacyCompetitorsListed: false);

    private static readonly VisitReportV2CheckContext PharmacyContext = new(
        VisitReportKinds.Pharmacy,
        new HashSet<Guid>(),
        new HashSet<Guid>(),
        TotalDue: false,
        new HashSet<Guid>(),
        PharmacyCompetitorsListed: true);

    private static VisitReportV2Body CompleteDoctor() => new(
        new VisitReportGeneral { VisitKindCode = "in_person", MetContactId = Guid.NewGuid() },
        new List<VisitReportProductSection>
        {
            new()
            {
                ProductId = ProductA, Minutes = 6, VisitSuccessCode = "reached", CommitmentCode = "will_try", InterestCode = "interested",
                AdoptionStageCode = "evaluating", ShareBucketCode = "occasional",
                Potential = new VisitReportPotential { Rows = { new VisitReportPotentialRow { IndicationNodeId = Guid.NewGuid(), MonthlyPatients = 12 } } },
                Competitors = { new VisitReportCompetitor { CompetitorProductId = Guid.NewGuid(), NameSnapshot = "Koronex 100" } },
                Objections = { new VisitReportObjection { CategoryCode = "price", Resolved = VisitReportObjectionResolutions.Partly } },
                Requests = { new VisitReportRequest { TypeCode = "literature" } },
                Sample = new VisitReportV2Sample { GskuId = Gsku, Quantity = 2 },
                NextMessage = "Deneme hastalarını sor"
            }
        },
        new VisitReportDoctorLevel { TotalMonthlyPatients = 120 },
        null,
        new VisitReportSafety { AdverseEvent = false, OffLabelRequest = false },
        new VisitReportFollowUp());

    private static VisitReportV2Body CompletePharmacy() => new(
        new VisitReportGeneral { VisitKindCode = "in_person", MetRoleCode = "pharmacist" },
        null,
        null,
        new VisitReportPharmacy
        {
            Stock = { new VisitReportStockRow { GskuId = Gsku, LevelCode = "ok" } },
            Sales = { new VisitReportSalesRow { GskuId = Gsku, TrendCode = "up" } },
            Competitors = { new VisitReportCompetitor { CompetitorProductId = Guid.NewGuid(), NameSnapshot = "Tensilox" } }
        },
        new VisitReportSafety { AdverseEvent = false },
        new VisitReportFollowUp());

    private static readonly Dictionary<string, (Action<VisitReportV2Body> Blank, string Section, bool PerProduct, string Field)> DoctorCases = new()
    {
        ["visitKind"] = (b => b.General!.VisitKindCode = null, VisitReportV2Sections.General, false, "visitKindCode"),
        ["metContact"] = (b => b.General!.MetContactId = null, VisitReportV2Sections.General, false, "metContactId"),
        ["adverseEvent"] = (b => b.Safety!.AdverseEvent = null, VisitReportV2Sections.Safety, false, "adverseEvent"),
        ["adverseEventNote"] = (b => { b.Safety!.AdverseEvent = true; b.Safety.AdverseEventNote = " "; }, VisitReportV2Sections.Safety, false, "adverseEventNote"),
        ["offLabel"] = (b => b.Safety!.OffLabelRequest = null, VisitReportV2Sections.Safety, false, "offLabelRequest"),
        ["totalPatients"] = (b => b.Doctor!.TotalMonthlyPatients = null, VisitReportV2Sections.Doctor, false, "totalMonthlyPatients"),
        ["minutes"] = (b => P(b).Minutes = null, VisitReportV2Sections.Product, true, "minutes"),
        ["visitSuccess"] = (b => P(b).VisitSuccessCode = null, VisitReportV2Sections.Product, true, "visitSuccessCode"),
        ["commitment"] = (b => P(b).CommitmentCode = " ", VisitReportV2Sections.Product, true, "commitmentCode"),
        ["interest"] = (b => P(b).InterestCode = null, VisitReportV2Sections.Product, true, "interestCode"),
        ["adoptionStage"] = (b => P(b).AdoptionStageCode = null, VisitReportV2Sections.Product, true, "adoptionStageCode"),
        ["shareBucket"] = (b => P(b).ShareBucketCode = null, VisitReportV2Sections.Product, true, "shareBucketCode"),
        ["potential"] = (b => P(b).Potential!.Rows.Clear(), VisitReportV2Sections.Product, true, "potential"),
        ["competitors"] = (b => P(b).Competitors.Clear(), VisitReportV2Sections.Product, true, "competitors"),
        ["objections"] = (b => P(b).Objections.Clear(), VisitReportV2Sections.Product, true, "objections"),
        ["objectionCategory"] = (b => P(b).Objections[0].CategoryCode = null, VisitReportV2Sections.Product, true, "objections.categoryCode"),
        ["objectionResolved"] = (b => P(b).Objections[0].Resolved = "maybe", VisitReportV2Sections.Product, true, "objections.resolved"),
        ["requests"] = (b => P(b).Requests.Clear(), VisitReportV2Sections.Product, true, "requests"),
        ["requestType"] = (b => P(b).Requests[0].TypeCode = null, VisitReportV2Sections.Product, true, "requests.typeCode"),
        ["sample"] = (b => P(b).Sample = null, VisitReportV2Sections.Product, true, "sample"),
        ["nextMessage"] = (b => P(b).NextMessage = "", VisitReportV2Sections.Product, true, "nextMessage"),
    };

    private static readonly Dictionary<string, (Action<VisitReportV2Body> Blank, string Section, bool PerGsku, string Field)> PharmacyCases = new()
    {
        ["visitKind"] = (b => b.General!.VisitKindCode = null, VisitReportV2Sections.General, false, "visitKindCode"),
        ["metRole"] = (b => b.General!.MetRoleCode = null, VisitReportV2Sections.General, false, "metRoleCode"),
        ["adverseEvent"] = (b => b.Safety!.AdverseEvent = null, VisitReportV2Sections.Safety, false, "adverseEvent"),
        ["stockLevel"] = (b => b.Pharmacy!.Stock[0].LevelCode = null, VisitReportV2Sections.Pharmacy, true, "stock.levelCode"),
        ["salesTrend"] = (b => b.Pharmacy!.Sales[0].TrendCode = " ", VisitReportV2Sections.Pharmacy, true, "sales.trendCode"),
        ["competitors"] = (b => b.Pharmacy!.Competitors.Clear(), VisitReportV2Sections.Pharmacy, false, "competitors"),
    };

    public static TheoryData<string> DoctorCaseNames() => new(DoctorCases.Keys);

    public static TheoryData<string> PharmacyCaseNames() => new(PharmacyCases.Keys);

    private static VisitReportProductSection P(VisitReportV2Body b) => b.ProductSections![0];

    [Fact]
    public void A_complete_doctor_report_and_a_complete_pharmacy_report_miss_nothing()
    {
        Assert.Empty(VisitReportV2Rules.Missing(CompleteDoctor(), DoctorContext));
        Assert.Empty(VisitReportV2Rules.Missing(CompletePharmacy(), PharmacyContext));
    }

    [Theory]
    [MemberData(nameof(DoctorCaseNames))]
    public void Blanking_one_doctor_answer_misses_exactly_that_answer(string name)
    {
        var (blank, section, perProduct, field) = DoctorCases[name];
        var body = CompleteDoctor();
        blank(body);

        var missing = VisitReportV2Rules.Missing(body, DoctorContext);

        var only = Assert.Single(missing);
        Assert.Equal((section, perProduct ? ProductA : (Guid?)null, field), (only.Section, only.ProductId, only.Field));
    }

    [Theory]
    [MemberData(nameof(PharmacyCaseNames))]
    public void Blanking_one_pharmacy_answer_misses_exactly_that_answer(string name)
    {
        var (blank, section, perGsku, field) = PharmacyCases[name];
        var body = CompletePharmacy();
        blank(body);

        var missing = VisitReportV2Rules.Missing(body, PharmacyContext);

        var only = Assert.Single(missing);
        Assert.Equal((section, perGsku ? Gsku : (Guid?)null, field), (only.Section, only.ProductId, only.Field));
    }

    [Fact]
    public void The_none_answers_stand_in_for_the_lists()
    {
        var body = CompleteDoctor();
        var s = P(body);
        s.Competitors.Clear(); s.NoCompetitor = true;
        s.Objections.Clear(); s.NoObjection = true;
        s.Requests.Clear(); s.NoRequest = true;
        s.Sample = null; s.NoSample = true;
        s.Potential!.Rows.Clear(); s.Potential.NoPatientsInIndications = true;
        var pharmacy = CompletePharmacy();
        pharmacy.Pharmacy!.Competitors.Clear(); pharmacy.Pharmacy.NoCompetitor = true;

        Assert.Empty(VisitReportV2Rules.Missing(body, DoctorContext));
        Assert.Empty(VisitReportV2Rules.Missing(pharmacy, PharmacyContext));
    }

    [Fact]
    public void Every_required_check_of_the_rules_has_a_case_here()
    {
        // A new Need(...) in VisitReportV2Rules.Missing without a case above turns this red: the guard stays complete.
        var source = File.ReadAllText(RulesPath());
        var start = source.IndexOf("public static IReadOnlyList<VisitReportMissingDto> Missing(", StringComparison.Ordinal);
        var end = source.IndexOf("public static string? Shape(", start, StringComparison.Ordinal);
        var fields = System.Text.RegularExpressions.Regex.Matches(source[start..end], "\"([a-zA-Z.]+)\"\\);")
            .Select(m => m.Groups[1].Value).Where(f => f != "section").ToHashSet();
        var covered = DoctorCases.Values.Select(c => c.Field).Concat(PharmacyCases.Values.Select(c => c.Field)).ToHashSet();
        Assert.Empty(fields.Except(covered));
    }

    private static string RulesPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.CrmService"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"),
            "services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "VisitReport", "V2", "VisitReportV2Rules.cs");
    }
}
