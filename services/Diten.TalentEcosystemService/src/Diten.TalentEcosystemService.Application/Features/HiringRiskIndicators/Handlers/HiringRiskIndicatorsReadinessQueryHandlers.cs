using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Handlers;

public sealed class GetHiringRiskIndicatorsReadinessListHandler
    : IRequestHandler<GetHiringRiskIndicatorsReadinessListQuery, Response<IReadOnlyList<HiringRiskIndicatorsReadinessListItemDto>>>
{
    private readonly IHiringRiskIndicatorsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHiringRiskIndicatorsReadinessListHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<HiringRiskIndicatorsReadinessListItemDto>>> Handle(
        GetHiringRiskIndicatorsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<HiringRiskIndicatorsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<HiringRiskIndicatorsReadinessListItemDto>>.Success(rows.Select(HiringRiskIndicatorsMapper.ToListItem).ToList());
    }
}

public sealed class GetHiringRiskIndicatorsReadinessByIdHandler
    : IRequestHandler<GetHiringRiskIndicatorsReadinessByIdQuery, Response<HiringRiskIndicatorsReadinessDto>>
{
    private readonly IHiringRiskIndicatorsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHiringRiskIndicatorsReadinessByIdHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HiringRiskIndicatorsReadinessDto>> Handle(GetHiringRiskIndicatorsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HiringRiskIndicatorsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<HiringRiskIndicatorsReadinessDto>.Fail("HiringRiskIndicators readiness record was not found.", 404)
            : Response<HiringRiskIndicatorsReadinessDto>.Success(HiringRiskIndicatorsMapper.ToDto(entity));
    }
}

public sealed class GetHiringRiskIndicatorsAuditMetadataHandler
    : IRequestHandler<GetHiringRiskIndicatorsAuditMetadataQuery, Response<HiringRiskIndicatorsAuditMetadataDto>>
{
    private readonly IHiringRiskIndicatorsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHiringRiskIndicatorsAuditMetadataHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HiringRiskIndicatorsAuditMetadataDto>> Handle(GetHiringRiskIndicatorsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HiringRiskIndicatorsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<HiringRiskIndicatorsAuditMetadataDto>.Fail("HiringRiskIndicators readiness record was not found.", 404)
            : Response<HiringRiskIndicatorsAuditMetadataDto>.Success(HiringRiskIndicatorsMapper.ToAuditMetadata(entity));
    }
}
