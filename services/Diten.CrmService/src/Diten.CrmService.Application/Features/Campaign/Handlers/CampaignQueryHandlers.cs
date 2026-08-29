using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Features.Campaign.Handlers;

public sealed class ListCampaignsHandler : IRequestHandler<ListCampaignsQuery, Response<CampaignListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICampaignRepository _repository;
    private readonly ICyclePeriodReader _cyclePeriods;
    private readonly ICampaignSegmentCatalog _segments;

    public ListCampaignsHandler(
        ITenantContext tenant,
        ICampaignRepository repository,
        ICyclePeriodReader cyclePeriods,
        ICampaignSegmentCatalog segments)
    {
        _tenant = tenant;
        _repository = repository;
        _cyclePeriods = cyclePeriods;
        _segments = segments;
    }

    public async Task<Response<CampaignListDto>> Handle(ListCampaignsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CampaignListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<CampaignEntity> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.CampaignType))
        {
            var campaignType = CampaignTypes.Normalize(request.CampaignType);
            rows = rows.Where(c => c.CampaignType == campaignType);
        }

        if (!string.IsNullOrWhiteSpace(request.CampaignStatus))
        {
            var campaignStatus = CampaignStatuses.Normalize(request.CampaignStatus);
            rows = rows.Where(c => c.CampaignStatus == campaignStatus);
        }

        // FU10 - the brand / product / subject filters are gone with the fields they filtered: nobody can author
        // them any more, and offering a filter over a field the UI cannot set advertises a capability that no longer
        // exists.
        if (!string.IsNullOrWhiteSpace(request.TargetingMode))
        {
            var targetingMode = CampaignTargetingModes.Normalize(request.TargetingMode);
            rows = rows.Where(c => string.Equals(c.EffectiveTargetingMode(), targetingMode, StringComparison.Ordinal));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(c => !c.IsArchived());
        }

        var page = rows.ToList();

        // FU08 — resolve every bound period in ONE read, then project. Calling the seam per row would be an N+1 over
        // the grid. The projection is display-only and is never written back onto a campaign.
        var boundIds = page
            .Where(c => c.CyclePeriodId is { } id && id != Guid.Empty)
            .Select(c => c.CyclePeriodId!.Value)
            .Distinct()
            .ToList();

        var periods = boundIds.Count == 0
            ? new Dictionary<Guid, CampaignCyclePeriodDto>()
            : (await _cyclePeriods.GetByIdsAsync(boundIds, cancellationToken))
                .ToDictionary(p => p.CyclePeriodId, CampaignMapper.ToCyclePeriodDto);

        // FU10 - resolve every targeted segment in ONE read too, for the same reason. Display-only; nothing here
        // is written back onto a campaign.
        var segmentIds = page
            .SelectMany(c => c.TargetedSegments.Select(s => s.SegmentId))
            .Distinct()
            .ToList();

        var segments = segmentIds.Count == 0
            ? new Dictionary<Guid, CampaignSegmentRef>()
            : (await _segments.GetByIdsAsync(segmentIds, cancellationToken)).ToDictionary(s => s.SegmentId);

        var items = page
            .Select(c => CampaignMapper.ToDto(
                c,
                c.CyclePeriodId is { } id && periods.TryGetValue(id, out var period) ? period : null,
                segments))
            .ToList();
        return Response<CampaignListDto>.Success(new CampaignListDto(items, items.Count));
    }
}

public sealed class GetCampaignHandler : IRequestHandler<GetCampaignQuery, Response<CampaignDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICampaignRepository _repository;
    private readonly ICyclePeriodReader _cyclePeriods;
    private readonly ICampaignSegmentCatalog _segments;

    public GetCampaignHandler(
        ITenantContext tenant,
        ICampaignRepository repository,
        ICyclePeriodReader cyclePeriods,
        ICampaignSegmentCatalog segments)
    {
        _tenant = tenant;
        _repository = repository;
        _cyclePeriods = cyclePeriods;
        _segments = segments;
    }

    public async Task<Response<CampaignDto>> Handle(GetCampaignQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CampaignDto>.Fail("Tenant context is required.", 400);
        }

        var campaign = await _repository.GetByIdAsync(tenantId, request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Response<CampaignDto>.Fail("Campaign not found.", 404);
        }

        // FU08 — display projection only. A period that cannot be read leaves the projection null rather than
        // failing the read: a detail page must still open, and a dangling reference is refused on the WRITE path.
        CampaignCyclePeriodDto? cyclePeriod = null;
        if (campaign.CyclePeriodId is { } cyclePeriodId && cyclePeriodId != Guid.Empty)
        {
            var period = await _cyclePeriods.GetByIdAsync(cyclePeriodId, cancellationToken);
            cyclePeriod = period is null ? null : CampaignMapper.ToCyclePeriodDto(period);
        }

        // FU10 - the same display-only rule for segments: one that cannot be read leaves its projection empty and
        // the campaign still shows the id it pinned, rather than a label nobody can vouch for.
        var segmentIds = campaign.TargetedSegments.Select(s => s.SegmentId).Distinct().ToList();
        var segments = segmentIds.Count == 0
            ? new Dictionary<Guid, CampaignSegmentRef>()
            : (await _segments.GetByIdsAsync(segmentIds, cancellationToken)).ToDictionary(s => s.SegmentId);

        return Response<CampaignDto>.Success(CampaignMapper.ToDto(campaign, cyclePeriod, segments));
    }
}

public sealed class ListCampaignTargetsHandler
    : IRequestHandler<ListCampaignTargetsQuery, Response<CampaignTargetListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignTargetRepository _targets;

    public ListCampaignTargetsHandler(
        ITenantContext tenant, ICampaignRepository campaigns, ICampaignTargetRepository targets)
    {
        _tenant = tenant;
        _campaigns = campaigns;
        _targets = targets;
    }

    public async Task<Response<CampaignTargetListDto>> Handle(
        ListCampaignTargetsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CampaignTargetListDto>.Fail("Tenant context is required.", 400);
        }

        // An archived campaign's targets stay READABLE (only mutation is blocked), so this read does not gate on status.
        if (await _campaigns.GetByIdAsync(tenantId, request.CampaignId, cancellationToken) is null)
        {
            return Response<CampaignTargetListDto>.Fail("Campaign not found.", 404);
        }

        IEnumerable<CampaignTarget> rows =
            await _targets.ListByCampaignAsync(tenantId, request.CampaignId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TargetType))
        {
            var targetType = CampaignTargetTypes.Normalize(request.TargetType);
            rows = rows.Where(t => t.TargetType == targetType);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetStatus))
        {
            var targetStatus = CampaignTargetStatuses.Normalize(request.TargetStatus);
            rows = rows.Where(t => t.TargetStatus == targetStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetSource))
        {
            var targetSource = CampaignTargetSources.Normalize(request.TargetSource);
            rows = rows.Where(t => t.TargetSource == targetSource);
        }

        if (request.SnapshotBatchId is { } batchId && batchId != Guid.Empty)
        {
            rows = rows.Where(t => t.SnapshotBatchId == batchId);
        }

        // Excluded rows are deliberately included by default: an excluded target with its reason IS the audit trail.
        if (!request.IncludeArchived)
        {
            rows = rows.Where(t => !t.IsArchived());
        }

        var items = rows.Select(CampaignMapper.ToDto).ToList();
        return Response<CampaignTargetListDto>.Success(new CampaignTargetListDto(items, items.Count));
    }
}

public sealed class GetCampaignTargetHandler : IRequestHandler<GetCampaignTargetQuery, Response<CampaignTargetDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICampaignTargetRepository _targets;

    public GetCampaignTargetHandler(ITenantContext tenant, ICampaignTargetRepository targets)
    {
        _tenant = tenant;
        _targets = targets;
    }

    public async Task<Response<CampaignTargetDto>> Handle(
        GetCampaignTargetQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CampaignTargetDto>.Fail("Tenant context is required.", 400);
        }

        var target = await _targets.GetByIdAsync(tenantId, request.CampaignTargetId, cancellationToken);
        return target is null || target.CampaignId != request.CampaignId
            ? Response<CampaignTargetDto>.Fail("Campaign target not found.", 404)
            : Response<CampaignTargetDto>.Success(CampaignMapper.ToDto(target));
    }
}

/// <summary>
/// FU11 — the create form's CampaignCode placeholder. A READ: it consumes no sequence number and writes nothing, so
/// the long-standing "never generate when a form is opened" rule still holds. What it returns is a hint, and the
/// create path is untouched — an empty CampaignCode is still what asks the server to assign one at save.
/// </summary>
public sealed class PeekNextCampaignCodeHandler
    : IRequestHandler<PeekNextCampaignCodeQuery, Response<CampaignCodePeek>>
{
    private readonly ITenantContext _tenant;
    private readonly ICampaignCodeGenerator _codeGenerator;

    public PeekNextCampaignCodeHandler(ITenantContext tenant, ICampaignCodeGenerator codeGenerator)
    {
        _tenant = tenant;
        _codeGenerator = codeGenerator;
    }

    public async Task<Response<CampaignCodePeek>> Handle(
        PeekNextCampaignCodeQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CampaignCodePeek>.Fail("Tenant context is required.", 400);
        }

        var peek = await _codeGenerator.PeekAsync(tenantId, cancellationToken);

        // No free candidate within the budget is NOT an error here: creating still works, the form just opens with an
        // empty placeholder instead of a code that would not be the one assigned.
        return peek is null
            ? Response<CampaignCodePeek>.SuccessWithoutData()
            : Response<CampaignCodePeek>.Success(peek);
    }
}
