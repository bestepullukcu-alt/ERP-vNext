using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Handlers;

public sealed class GetCompensationBenefitsReadinessListHandler
    : IRequestHandler<GetCompensationBenefitsReadinessListQuery, Response<IReadOnlyList<CompensationBenefitsReadinessListItemDto>>>
{
    private readonly ICompensationBenefitsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCompensationBenefitsReadinessListHandler(ICompensationBenefitsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<CompensationBenefitsReadinessListItemDto>>> Handle(
        GetCompensationBenefitsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = CompensationBenefitsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<CompensationBenefitsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<CompensationBenefitsReadinessListItemDto>>.Success(rows.Select(CompensationBenefitsMapper.ToListItem).ToList());
    }
}

public sealed class GetCompensationBenefitsReadinessByIdHandler
    : IRequestHandler<GetCompensationBenefitsReadinessByIdQuery, Response<CompensationBenefitsReadinessDto>>
{
    private readonly ICompensationBenefitsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCompensationBenefitsReadinessByIdHandler(ICompensationBenefitsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CompensationBenefitsReadinessDto>> Handle(GetCompensationBenefitsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = CompensationBenefitsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CompensationBenefitsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CompensationBenefitsReadinessDto>.Fail("CompensationBenefits readiness record was not found.", 404)
            : Response<CompensationBenefitsReadinessDto>.Success(CompensationBenefitsMapper.ToDto(entity));
    }
}

public sealed class GetCompensationBenefitsAuditMetadataHandler
    : IRequestHandler<GetCompensationBenefitsAuditMetadataQuery, Response<CompensationBenefitsAuditMetadataDto>>
{
    private readonly ICompensationBenefitsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCompensationBenefitsAuditMetadataHandler(ICompensationBenefitsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CompensationBenefitsAuditMetadataDto>> Handle(GetCompensationBenefitsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CompensationBenefitsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CompensationBenefitsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CompensationBenefitsAuditMetadataDto>.Fail("CompensationBenefits readiness record was not found.", 404)
            : Response<CompensationBenefitsAuditMetadataDto>.Success(CompensationBenefitsMapper.ToAuditMetadata(entity));
    }
}
