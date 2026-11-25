using System.Globalization;
using Diten.PvgService.Domain.RegPvBase;
using MongoDB.Bson;

namespace Diten.PvgService.Persistence;

// The explicit mapping between SafetyCaseIntake and the stored document. Every domain property is named here, in both
// directions; a new domain property needs a line here (test Mongo_store_round_trips_every_SafetyCaseIntake_field).
internal static class PvgIntakeDraftDocumentMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    // The stored fields that are not domain fields: identity, version, deletion marker, audit stamps.
    private static readonly string[] NonDomainFields =
    [
        "_id", "IntakeDraftId", "Version", "IsDeleted", "DeletedAt", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy"
    ];

    public static PvgIntakeDraftDocument ToNewDocument(
        Guid draftGuid,
        string intakeDraftId,
        long version,
        string actorId,
        DateTime nowUtc,
        SafetyCaseIntake intake)
    {
        var document = new PvgIntakeDraftDocument
        {
            Id = draftGuid.ToString("D", CultureInfo.InvariantCulture),
            IntakeDraftId = intakeDraftId,
            Version = version,
            IsDeleted = false,
            DeletedAt = null,
            CreatedAt = nowUtc,
            CreatedBy = actorId
        };

        Fill(document, intake);
        document.UpdatedAt = nowUtc;
        document.UpdatedBy = actorId;
        return document;
    }

    // The mapped domain fields only. Identity, version, deletion marker and the Created* pair are not touched.
    public static void Fill(PvgIntakeDraftDocument document, SafetyCaseIntake intake)
    {
        document.TenantId = intake.TenantId;
        document.Status = intake.Status.ToString();
        document.IntakeChannel = intake.IntakeChannel;
        document.SourceType = intake.SourceType;
        document.SourceReference = intake.SourceReference;
        document.ReceivedAtUtc = intake.ReceivedAtUtc.UtcTicks;
        document.ReporterType = intake.ReporterType;
        document.ReporterContactSummary = intake.ReporterContactSummary;
        document.PatientSubjectCode = intake.PatientSubjectCode;
        document.EventOnsetDate = intake.EventOnsetDate?.ToString(DateFormat, CultureInfo.InvariantCulture);
        document.AdverseEventNarrative = intake.AdverseEventNarrative;
        document.Seriousness = intake.Seriousness;
        document.IntakePriority = intake.IntakePriority;
        document.TriageOutcome = intake.TriageOutcome?.ToString();
        document.TriageReason = intake.TriageReason;
        document.RouteTargetQueue = intake.RouteTargetQueue;
        document.EvidenceLinkReferences = intake.EvidenceLinkReferences.ToList();
        document.ProductLines = intake.ProductLines
            .Select(line => new PvgIntakeProductLineDocument
            {
                ProductRole = line.ProductRole,
                GlobalProductId = line.GlobalProductId,
                ProductNameAsReported = line.ProductNameAsReported,
                PresentationGskuId = line.PresentationGskuId,
                LotNumber = line.LotNumber
            })
            .ToList();
        document.AwarenessAtUtc = intake.AwarenessAtUtc.UtcTicks;
        document.CompanyRecipientPersonId = intake.CompanyRecipientPersonId;
        document.EventCountry = intake.EventCountry;
        document.ReportType = intake.ReportType;
        document.ReporterSource = intake.ReporterSource.ToString();
        document.ReporterSourceId = intake.ReporterSourceId;
        document.ReporterCrmSearchState = intake.ReporterCrmSearchState.ToString();
        document.ReporterNameOrInitials = intake.ReporterNameOrInitials;
        document.ReporterProfession = intake.ReporterProfession;
        document.ReporterCountry = intake.ReporterCountry;
    }

    // The $set body of a replace: the mapped domain fields only. The caller adds Version, UpdatedAt and UpdatedBy; a
    // replace never writes the identity, the deletion marker or the Created* pair.
    public static BsonDocument ToDomainFieldSet(SafetyCaseIntake intake)
    {
        var document = new PvgIntakeDraftDocument();
        Fill(document, intake);
        var fields = document.ToBsonDocument();
        foreach (var name in NonDomainFields)
        {
            fields.Remove(name);
        }

        return fields;
    }

    public static SafetyCaseIntake ToIntake(PvgIntakeDraftDocument document) =>
        new(
            document.TenantId,
            Enum.Parse<PvgIntakeStatus>(document.Status),
            document.IntakeChannel,
            document.SourceType,
            new DateTimeOffset(document.ReceivedAtUtc, TimeSpan.Zero),
            document.ReporterType,
            document.AdverseEventNarrative,
            document.Seriousness,
            document.IntakePriority,
            document.TriageOutcome is null ? null : Enum.Parse<PvgTriageOutcome>(document.TriageOutcome),
            document.TriageReason,
            document.RouteTargetQueue)
        {
            SourceReference = document.SourceReference,
            ReporterContactSummary = document.ReporterContactSummary,
            PatientSubjectCode = document.PatientSubjectCode,
            EventOnsetDate = document.EventOnsetDate is null
                ? null
                : DateOnly.ParseExact(document.EventOnsetDate, DateFormat, CultureInfo.InvariantCulture),
            EvidenceLinkReferences = document.EvidenceLinkReferences.ToArray(),
            ProductLines = document.ProductLines
                .Select(line => new PvgIntakeProductLine(
                    line.ProductRole,
                    line.GlobalProductId,
                    line.ProductNameAsReported,
                    line.PresentationGskuId,
                    line.LotNumber))
                .ToArray(),
            AwarenessAtUtc = new DateTimeOffset(document.AwarenessAtUtc, TimeSpan.Zero),
            CompanyRecipientPersonId = document.CompanyRecipientPersonId,
            EventCountry = document.EventCountry,
            ReportType = document.ReportType,
            ReporterSource = Enum.Parse<PvgReporterSource>(document.ReporterSource),
            ReporterSourceId = document.ReporterSourceId,
            ReporterCrmSearchState = Enum.Parse<PvgReporterCrmSearchState>(document.ReporterCrmSearchState),
            ReporterNameOrInitials = document.ReporterNameOrInitials,
            ReporterProfession = document.ReporterProfession,
            ReporterCountry = document.ReporterCountry
        };
}
