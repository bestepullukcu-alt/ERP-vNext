using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Api.Tests.Persistence;

// Synthetic, fake data only. A fresh tenant id per test isolates the tests that share one database.
internal static class PvgMongoTestData
{
    public static string NewTenant() => Guid.NewGuid().ToString("D");

    public static SafetyCaseIntake Intake(
        string tenantId,
        PvgIntakeStatus status = PvgIntakeStatus.IntakeCreated,
        DateTimeOffset? receivedAtUtc = null,
        string narrative = "narrative-fake")
    {
        var received = receivedAtUtc ?? new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
        return new SafetyCaseIntake(
            tenantId,
            status,
            "Portal",
            "Reporter",
            received,
            "HealthcareProfessional",
            narrative,
            "Serious",
            "High")
        {
            SourceReference = "source-ref-fake",
            ReporterContactSummary = "contact-fake",
            PatientSubjectCode = "patient-fake-1",
            EventOnsetDate = new DateOnly(2026, 2, 1),
            EvidenceLinkReferences = [],
            ProductLines = [new PvgIntakeProductLine("Suspect", null, "product-fake-a", null, "lot-1")],
            AwarenessAtUtc = received,
            EventCountry = "unknown",
            ReportType = "Spontaneous",
            ReporterSource = PvgReporterSource.Manual,
            ReporterSourceId = null,
            ReporterCrmSearchState = PvgReporterCrmSearchState.Pending,
            ReporterNameOrInitials = "J.D.",
            ReporterProfession = "Pharmacist",
            ReporterCountry = null
        };
    }

    // The replacement of FullIntake: every property except the tenant (a replace never moves a draft to another tenant)
    // carries a value that differs from FullIntake's, and is again not the default of its type. The round-trip test
    // checks the difference by reflection, so a property added to SafetyCaseIntake later that this fixture leaves equal
    // breaks the test.
    public static SafetyCaseIntake FullIntakeChanged(string tenantId) =>
        new(
            tenantId,
            PvgIntakeStatus.Routed,
            "Email",
            "Literature",
            new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero).AddTicks(2345678),
            "Consumer",
            "narrative-changed-fake",
            "NonSerious",
            "Low",
            PvgTriageOutcome.Rejected,
            "triage-reason-changed-fake",
            "route-queue-changed-fake")
        {
            SourceReference = "source-ref-changed-fake",
            ReporterContactSummary = "contact-changed-fake",
            PatientSubjectCode = "patient-changed-fake",
            EventOnsetDate = new DateOnly(2022, 8, 10),
            EvidenceLinkReferences = ["evidence-ref-changed-1", "evidence-ref-changed-2", "evidence-ref-changed-3"],
            ProductLines =
            [
                new PvgIntakeProductLine("Concomitant", "global-product-changed-1", "product-changed-a", "gsku-changed-1", "lot-changed-1")
            ],
            AwarenessAtUtc = new DateTimeOffset(2026, 6, 6, 7, 8, 9, TimeSpan.Zero).AddTicks(8765432),
            CompanyRecipientPersonId = "company-person-changed-fake",
            EventCountry = "country-event-changed-fake",
            ReportType = "Literature",
            ReporterSource = PvgReporterSource.Crm,
            ReporterSourceId = "reporter-source-changed-fake",
            ReporterCrmSearchState = PvgReporterCrmSearchState.Searched,
            ReporterNameOrInitials = "reporter-name-changed-fake",
            ReporterProfession = "profession-changed-fake",
            ReporterCountry = "country-reporter-changed-fake"
        };

    // Every property of SafetyCaseIntake carries a value that is not the default of its type; the instants carry
    // sub-millisecond ticks so that a store that cuts to milliseconds cannot return them unchanged.
    public static SafetyCaseIntake FullIntake(string tenantId) =>
        new(
            tenantId,
            PvgIntakeStatus.RoutePending,
            "Portal",
            "Reporter",
            new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero).AddTicks(1234567),
            "HealthcareProfessional",
            "narrative-full-fake",
            "Serious",
            "High",
            PvgTriageOutcome.Duplicate,
            "triage-reason-fake",
            "route-queue-fake")
        {
            SourceReference = "source-ref-full-fake",
            ReporterContactSummary = "contact-full-fake",
            PatientSubjectCode = "patient-full-fake",
            EventOnsetDate = new DateOnly(2021, 7, 9),
            EvidenceLinkReferences = ["evidence-ref-1", "evidence-ref-2"],
            ProductLines =
            [
                new PvgIntakeProductLine("Suspect", "global-product-fake-1", "product-full-fake-a", "gsku-fake-1", "lot-full-1"),
                new PvgIntakeProductLine("Concomitant", "global-product-fake-2", "product-full-fake-b", "gsku-fake-2", "lot-full-2")
            ],
            AwarenessAtUtc = new DateTimeOffset(2026, 5, 5, 6, 7, 8, TimeSpan.Zero).AddTicks(7654321),
            CompanyRecipientPersonId = "company-person-fake",
            EventCountry = "country-event-fake",
            ReportType = "Study",
            ReporterSource = PvgReporterSource.PersonDirectory,
            ReporterSourceId = "reporter-source-fake",
            ReporterCrmSearchState = PvgReporterCrmSearchState.ClosedByUserUnlink,
            ReporterNameOrInitials = "reporter-name-fake",
            ReporterProfession = "profession-fake",
            ReporterCountry = "country-reporter-fake"
        };
}
