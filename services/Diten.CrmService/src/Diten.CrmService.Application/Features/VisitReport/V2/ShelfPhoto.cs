using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.V2;

/// <summary>The stored shelf photo: its MOD-0262 content id goes into the draft's <c>pharmacy.shelf.photoEvidenceId</c>.</summary>
public sealed record ShelfPhotoDto(Guid PhotoEvidenceId, string MediaType, long ByteSize);

/// <summary>WP-VW-W4 (W4-WEB-b, BE-a §37 open 2) — <c>POST api/crm/visit-report/v2/{plannedVisitId}/shelf-photo</c>.</summary>
public sealed record UploadShelfPhotoCommand(Guid PlannedVisitId, VisitEvidenceFile? File) : IRequest<Response<ShelfPhotoDto>>;

/// <summary><c>GET api/crm/visit-report/v2/{plannedVisitId}/shelf-photo</c> — the photo the visit's report points at.</summary>
public sealed record GetShelfPhotoQuery(Guid PlannedVisitId) : IRequest<Response<ContentArtifactReadResult>>;

public static class ShelfPhotoErrors
{
    public const string PharmacyOnly = "shelf_photo_pharmacy_only";
    public const string AlreadyReported = "shelf_photo_report_submitted";
    public const string NotFound = "shelf_photo_not_found";
}

/// <summary>
/// WP-VW-W4 (W4-WEB-b) — the pharmacy report's shelf photo, in the W3 start-photo pattern: the caller's OWN planned visit
/// (or <c>crm.planned-visit.read-all</c>), a PHARMACY plan only, its report not yet submitted (a correction after that is an
/// amendment), a JPEG / PNG of at most 10 MB (the Platform <c>CrmVisitEvidence</c> scope rule, checked before any upload),
/// stored in MOD-0262 under that scope AS the signed-in user (the repository's <c>visit-evidence.write</c> decides). The
/// owning version is the visit's execution (else the plan). Nothing else is written: the Web puts the returned id in the
/// draft.
/// </summary>
public sealed class UploadShelfPhotoHandler : IRequestHandler<UploadShelfPhotoCommand, Response<ShelfPhotoDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IVisitExecutionRepository _executions;
    private readonly IVisitEvidenceStore _store;

    public UploadShelfPhotoHandler(ITenantContext tenant, ICallerScope caller, IPlannedVisitRepository plans,
        IVisitReportRepository reports, IVisitExecutionRepository executions, IVisitEvidenceStore store)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _reports = reports;
        _executions = executions;
        _store = store;
    }

    public async Task<Response<ShelfPhotoDto>> Handle(UploadShelfPhotoCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<ShelfPhotoDto>.Fail("Tenant context is required.", 400);

        var plan = await _plans.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null)
        {
            return VisitReportV2Service.Fail<ShelfPhotoDto>(Contract.VisitReportErrorCodes.PlannedVisitNotFound, "The planned visit does not exist.", 404);
        }

        if (VisitExecutionAccess.WriteRefusal(_caller, plan) is { } ownership)
        {
            return VisitExecutionAccess.Fail<ShelfPhotoDto>(ownership);
        }

        if (VisitReportV2Rules.KindOf(plan) != VisitReportKinds.Pharmacy)
        {
            return VisitReportV2Service.Fail<ShelfPhotoDto>(ShelfPhotoErrors.PharmacyOnly, "A shelf photo belongs to a pharmacy visit.", 400);
        }

        if ((await _reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken))?.IsFinalised() == true)
        {
            return VisitReportV2Service.Fail<ShelfPhotoDto>(ShelfPhotoErrors.AlreadyReported,
                "The report is already submitted; a new photo goes with an amendment.", 409);
        }

        if (request.File is null || VisitExecutionRules.ValidatePhoto(request.File) is not null)
        {
            return VisitReportV2Service.Fail<ShelfPhotoDto>(VisitExecutionErrorCodes.PhotoInvalid,
                "The photo must be a JPEG or PNG image of at most 10 MB.", 400);
        }

        var execution = await _executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken);
        try
        {
            var stored = await _store.StoreAsync(
                new VisitEvidenceStoreRequest(plan.Id, execution?.Id ?? plan.Id, request.File.FileName, request.File.MediaType, request.File.Content),
                cancellationToken);
            return Response<ShelfPhotoDto>.Success(new ShelfPhotoDto(stored.ContentId, stored.MediaType, stored.ByteSize), 201);
        }
        catch (ContentArtifactStoreException ex)
        {
            return VisitExecutionAccess.Fail<ShelfPhotoDto>(StartVisitExecutionHandler.StoreFailure(ex, VisitExecutionErrorCodes.PhotoInvalid));
        }
    }
}

/// <summary>The shelf photo the visit's report points at — never an arbitrary content id (the id is read from the report).</summary>
public sealed class GetShelfPhotoHandler : IRequestHandler<GetShelfPhotoQuery, Response<ContentArtifactReadResult>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IVisitEvidenceStore _store;

    public GetShelfPhotoHandler(ITenantContext tenant, ICallerScope caller, IPlannedVisitRepository plans, IVisitReportRepository reports,
        IVisitEvidenceStore store)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _reports = reports;
        _store = store;
    }

    public async Task<Response<ContentArtifactReadResult>> Handle(GetShelfPhotoQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<ContentArtifactReadResult>.Fail("Tenant context is required.", 400);
        var plan = await _plans.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null || !_caller.MayAccess(PlannedVisit.PlannedVisitPermissions.ReadAll, plan.Resource.ResourceId))
        {
            return NotFound();
        }

        var id = (await _reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken))?.Pharmacy?.Shelf?.PhotoEvidenceId;
        if (id is not { } contentId) return NotFound();
        var read = await _store.OpenReadAsync(contentId, cancellationToken);
        return read is null ? NotFound() : Response<ContentArtifactReadResult>.Success(read with { FileName = "shelf-photo" + (read.MediaType == "image/png" ? ".png" : ".jpg") });
    }

    private static Response<ContentArtifactReadResult> NotFound()
        => VisitReportV2Service.Fail<ContentArtifactReadResult>(ShelfPhotoErrors.NotFound, "There is no shelf photo for this visit.", 404);
}
