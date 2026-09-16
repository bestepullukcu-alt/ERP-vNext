using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Handlers;

public sealed class GetSectorMobilityIntelligenceReadinessListHandler
    : IRequestHandler<GetSectorMobilityIntelligenceReadinessListQuery, Response<IReadOnlyList<SectorMobilityIntelligenceReadinessListItemDto>>>
{
    private readonly ISectorMobilityIntelligenceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetSectorMobilityIntelligenceReadinessListHandler(ISectorMobilityIntelligenceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<SectorMobilityIntelligenceReadinessListItemDto>>> Handle(
        GetSectorMobilityIntelligenceReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = SectorMobilityIntelligenceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<SectorMobilityIntelligenceReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<SectorMobilityIntelligenceReadinessListItemDto>>.Success(rows.Select(SectorMobilityIntelligenceMapper.ToListItem).ToList());
    }
}

public sealed class GetSectorMobilityIntelligenceReadinessByIdHandler
    : IRequestHandler<GetSectorMobilityIntelligenceReadinessByIdQuery, Response<SectorMobilityIntelligenceReadinessDto>>
{
    private readonly ISectorMobilityIntelligenceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetSectorMobilityIntelligenceReadinessByIdHandler(ISectorMobilityIntelligenceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SectorMobilityIntelligenceReadinessDto>> Handle(GetSectorMobilityIntelligenceReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = SectorMobilityIntelligenceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorMobilityIntelligenceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<SectorMobilityIntelligenceReadinessDto>.Fail("SectorMobilityIntelligence readiness record was not found.", 404)
            : Response<SectorMobilityIntelligenceReadinessDto>.Success(SectorMobilityIntelligenceMapper.ToDto(entity));
    }
}

public sealed class GetSectorMobilityIntelligenceAuditMetadataHandler
    : IRequestHandler<GetSectorMobilityIntelligenceAuditMetadataQuery, Response<SectorMobilityIntelligenceAuditMetadataDto>>
{
    private readonly ISectorMobilityIntelligenceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetSectorMobilityIntelligenceAuditMetadataHandler(ISectorMobilityIntelligenceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SectorMobilityIntelligenceAuditMetadataDto>> Handle(GetSectorMobilityIntelligenceAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = SectorMobilityIntelligenceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorMobilityIntelligenceAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<SectorMobilityIntelligenceAuditMetadataDto>.Fail("SectorMobilityIntelligence readiness record was not found.", 404)
            : Response<SectorMobilityIntelligenceAuditMetadataDto>.Success(SectorMobilityIntelligenceMapper.ToAuditMetadata(entity));
    }
}
