using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Handlers;

public sealed class GetSkillsGapHeatmapReadinessListHandler
    : IRequestHandler<GetSkillsGapHeatmapReadinessListQuery, Response<IReadOnlyList<SkillsGapHeatmapReadinessListItemDto>>>
{
    private readonly ISkillsGapHeatmapReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSkillsGapHeatmapReadinessListHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<SkillsGapHeatmapReadinessListItemDto>>> Handle(
        GetSkillsGapHeatmapReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<SkillsGapHeatmapReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<SkillsGapHeatmapReadinessListItemDto>>.Success(rows.Select(SkillsGapHeatmapMapper.ToListItem).ToList());
    }
}

public sealed class GetSkillsGapHeatmapReadinessByIdHandler
    : IRequestHandler<GetSkillsGapHeatmapReadinessByIdQuery, Response<SkillsGapHeatmapReadinessDto>>
{
    private readonly ISkillsGapHeatmapReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSkillsGapHeatmapReadinessByIdHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SkillsGapHeatmapReadinessDto>> Handle(GetSkillsGapHeatmapReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SkillsGapHeatmapReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<SkillsGapHeatmapReadinessDto>.Fail("SkillsGapHeatmap readiness record was not found.", 404)
            : Response<SkillsGapHeatmapReadinessDto>.Success(SkillsGapHeatmapMapper.ToDto(entity));
    }
}

public sealed class GetSkillsGapHeatmapAuditMetadataHandler
    : IRequestHandler<GetSkillsGapHeatmapAuditMetadataQuery, Response<SkillsGapHeatmapAuditMetadataDto>>
{
    private readonly ISkillsGapHeatmapReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSkillsGapHeatmapAuditMetadataHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SkillsGapHeatmapAuditMetadataDto>> Handle(GetSkillsGapHeatmapAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SkillsGapHeatmapAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<SkillsGapHeatmapAuditMetadataDto>.Fail("SkillsGapHeatmap readiness record was not found.", 404)
            : Response<SkillsGapHeatmapAuditMetadataDto>.Success(SkillsGapHeatmapMapper.ToAuditMetadata(entity));
    }
}
