using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Application.Tests.VisitReport;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using ReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W4 · W4-WEB-b (BE-a §37 open 2) — the pharmacy shelf photo on the PRODUCTION handlers: a pharmacy plan of the
/// caller only, the report not yet submitted, JPEG / PNG ≤ 10 MB before any upload, stored under the visit (MOD-0262
/// CrmVisitEvidence via the evidence store), read back only through the visit's report (never an arbitrary id).
/// </summary>
public sealed class VwW4ShelfPhotoTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000b4");
    private const string Rep = "rep-1";
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };

    [Fact]
    public async Task A_pharmacy_rep_stores_a_jpeg_under_the_visit_and_gets_its_id()
    {
        var w = new World();
        var plan = w.Plan(PlannedVisitTargetType.Pharmacy);
        var execution = new Domain.Entities.VisitExecution { TenantId = Tenant, PlannedVisitId = plan.Id, ResourceId = Rep, Status = VisitExecutionStatus.Finished };
        w.Executions.Items.Add(execution);

        var ok = await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(plan.Id, new VisitEvidenceFile("raf.jpg", "image/jpeg", Jpeg)), default);
        Assert.Equal(201, ok.StatusCode);
        var stored = Assert.Single(w.Store.Stored);
        Assert.Equal((stored.ContentId, "image/jpeg", 7L), (ok.Data!.PhotoEvidenceId, ok.Data.MediaType, ok.Data.ByteSize));
        Assert.Equal((plan.Id, execution.Id), (stored.Request.PlannedVisitId, stored.Request.ExecutionId));
    }

    [Fact]
    public async Task A_doctor_visit_another_reps_visit_a_bad_file_and_a_submitted_report_are_refused_before_any_upload()
    {
        var w = new World();
        var doctor = w.Plan(PlannedVisitTargetType.Contact);
        var pharmacy = w.Plan(PlannedVisitTargetType.Pharmacy);
        var foreign = w.Plan(PlannedVisitTargetType.Pharmacy, resource: "rep-2");

        var file = new VisitEvidenceFile("raf.jpg", "image/jpeg", Jpeg);
        var notPharmacy = await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(doctor.Id, file), default);
        Assert.Equal(400, notPharmacy.StatusCode);
        Assert.Contains(ShelfPhotoErrors.PharmacyOnly, notPharmacy.Errors!);
        Assert.Equal(404, (await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(foreign.Id, file), default)).StatusCode);
        var gif = await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(pharmacy.Id, new VisitEvidenceFile("raf.gif", "image/gif", Jpeg)), default);
        Assert.Contains(VisitExecutionErrorCodes.PhotoInvalid, gif.Errors!);
        Assert.Equal(400, (await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(pharmacy.Id, null), default)).StatusCode);
        var big = new VisitEvidenceFile("raf.jpg", "image/jpeg", new byte[VisitExecutionLimits.MaxEvidenceBytes + 1]);
        Assert.Equal(400, (await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(pharmacy.Id, big), default)).StatusCode);

        w.Reports.Items.Add(new ReportEntity { TenantId = Tenant, PlannedVisitId = pharmacy.Id, ReportStatus = VisitReportStatus.Submitted });
        var late = await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(pharmacy.Id, file), default);
        Assert.Equal(409, late.StatusCode);
        Assert.Empty(w.Store.Stored);
    }

    [Fact]
    public async Task The_photo_is_read_only_through_the_visits_report()
    {
        var w = new World();
        var plan = w.Plan(PlannedVisitTargetType.Pharmacy);
        Assert.Equal(404, (await w.Read(Rep).Handle(new GetShelfPhotoQuery(plan.Id), default)).StatusCode); // no report yet

        var id = (await w.Upload(Rep).Handle(new UploadShelfPhotoCommand(plan.Id, new VisitEvidenceFile("raf.png", "image/png", Jpeg)), default)).Data!.PhotoEvidenceId;
        w.Reports.Items.Add(new ReportEntity
        {
            TenantId = Tenant, PlannedVisitId = plan.Id, ReportStatus = VisitReportStatus.Draft,
            Pharmacy = new VisitReportPharmacy { Shelf = new VisitReportShelf { PhotoEvidenceId = id } }
        });
        var read = await w.Read(Rep).Handle(new GetShelfPhotoQuery(plan.Id), default);
        Assert.Equal(("image/png", "shelf-photo.png"), (read.Data!.MediaType, read.Data.FileName));
        Assert.Equal(404, (await w.Read("rep-2").Handle(new GetShelfPhotoQuery(plan.Id), default)).StatusCode); // another rep
    }

    private sealed class World
    {
        public FakePlannedVisitRepository Plans { get; } = new();
        public FakeVisitReportRepository Reports { get; } = new();
        public FakeVisitExecutionRepository Executions { get; } = new();
        public FakeVisitEvidenceStore Store { get; } = new();

        private static TenantContext Ctx()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(Tenant);
            return ctx;
        }

        public UploadShelfPhotoHandler Upload(string caller) => new(Ctx(), new TestCallerScope(caller), Plans, Reports, Executions, Store);

        public GetShelfPhotoHandler Read(string caller) => new(Ctx(), new TestCallerScope(caller), Plans, Reports, Store);

        public PlanAtom Plan(string targetType, string resource = Rep)
        {
            var plan = new PlanAtom
            {
                TenantId = Tenant, VisitCode = "V-" + Guid.NewGuid().ToString("N")[..6], TargetType = targetType, TargetId = Guid.NewGuid(),
                PlannedDate = DateOnly.FromDateTime(DateTime.UtcNow), Resource = new PlannedVisitResourceRef { ResourceId = resource },
                PlanStatus = PlannedVisitStatus.Planned
            };
            Plans.Items.Add(plan);
            return plan;
        }
    }
}
