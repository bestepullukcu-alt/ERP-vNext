using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Handlers;

public sealed class GetDevelopmentPlanReadinessListHandler
    : IRequestHandler<GetDevelopmentPlanReadinessListQuery, Response<IReadOnlyList<DevelopmentPlanReadinessListItemDto>>>
{
    private readonly IDevelopmentPlanReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetDevelopmentPlanReadinessListHandler(IDevelopmentPlanReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<DevelopmentPlanReadinessListItemDto>>> Handle(
        GetDevelopmentPlanReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = DevelopmentPlanGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<DevelopmentPlanReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<DevelopmentPlanReadinessListItemDto>>.Success(rows.Select(DevelopmentPlanMapper.ToListItem).ToList());
    }
}

public sealed class GetDevelopmentPlanReadinessByIdHandler
    : IRequestHandler<GetDevelopmentPlanReadinessByIdQuery, Response<DevelopmentPlanReadinessDto>>
{
    private readonly IDevelopmentPlanReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetDevelopmentPlanReadinessByIdHandler(IDevelopmentPlanReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<DevelopmentPlanReadinessDto>> Handle(GetDevelopmentPlanReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = DevelopmentPlanGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<DevelopmentPlanReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<DevelopmentPlanReadinessDto>.Fail("DevelopmentPlan readiness record was not found.", 404)
            : Response<DevelopmentPlanReadinessDto>.Success(DevelopmentPlanMapper.ToDto(entity));
    }
}

public sealed class GetDevelopmentPlanAuditMetadataHandler
    : IRequestHandler<GetDevelopmentPlanAuditMetadataQuery, Response<DevelopmentPlanAuditMetadataDto>>
{
    private readonly IDevelopmentPlanReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetDevelopmentPlanAuditMetadataHandler(IDevelopmentPlanReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<DevelopmentPlanAuditMetadataDto>> Handle(GetDevelopmentPlanAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = DevelopmentPlanGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<DevelopmentPlanAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<DevelopmentPlanAuditMetadataDto>.Fail("DevelopmentPlan readiness record was not found.", 404)
            : Response<DevelopmentPlanAuditMetadataDto>.Success(DevelopmentPlanMapper.ToAuditMetadata(entity));
    }
}
