using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Handlers;

public sealed class GetHrComplianceReadinessListHandler
    : IRequestHandler<GetHrComplianceReadinessListQuery, Response<IReadOnlyList<HrComplianceReadinessListItemDto>>>
{
    private readonly IHrComplianceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHrComplianceReadinessListHandler(IHrComplianceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<HrComplianceReadinessListItemDto>>> Handle(
        GetHrComplianceReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = HrComplianceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<HrComplianceReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<HrComplianceReadinessListItemDto>>.Success(rows.Select(HrComplianceMapper.ToListItem).ToList());
    }
}

public sealed class GetHrComplianceReadinessByIdHandler
    : IRequestHandler<GetHrComplianceReadinessByIdQuery, Response<HrComplianceReadinessDto>>
{
    private readonly IHrComplianceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHrComplianceReadinessByIdHandler(IHrComplianceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HrComplianceReadinessDto>> Handle(GetHrComplianceReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = HrComplianceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrComplianceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<HrComplianceReadinessDto>.Fail("HrCompliance readiness record was not found.", 404)
            : Response<HrComplianceReadinessDto>.Success(HrComplianceMapper.ToDto(entity));
    }
}

public sealed class GetHrComplianceAuditMetadataHandler
    : IRequestHandler<GetHrComplianceAuditMetadataQuery, Response<HrComplianceAuditMetadataDto>>
{
    private readonly IHrComplianceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHrComplianceAuditMetadataHandler(IHrComplianceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HrComplianceAuditMetadataDto>> Handle(GetHrComplianceAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = HrComplianceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrComplianceAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<HrComplianceAuditMetadataDto>.Fail("HrCompliance readiness record was not found.", 404)
            : Response<HrComplianceAuditMetadataDto>.Success(HrComplianceMapper.ToAuditMetadata(entity));
    }
}
