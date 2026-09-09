using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Handlers;

public sealed class GetPerformanceReviewReadinessListHandler
    : IRequestHandler<GetPerformanceReviewReadinessListQuery, Response<IReadOnlyList<PerformanceReviewReadinessListItemDto>>>
{
    private readonly IPerformanceReviewReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetPerformanceReviewReadinessListHandler(IPerformanceReviewReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<PerformanceReviewReadinessListItemDto>>> Handle(
        GetPerformanceReviewReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = PerformanceReviewGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<PerformanceReviewReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<PerformanceReviewReadinessListItemDto>>.Success(rows.Select(PerformanceReviewMapper.ToListItem).ToList());
    }
}

public sealed class GetPerformanceReviewReadinessByIdHandler
    : IRequestHandler<GetPerformanceReviewReadinessByIdQuery, Response<PerformanceReviewReadinessDto>>
{
    private readonly IPerformanceReviewReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetPerformanceReviewReadinessByIdHandler(IPerformanceReviewReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<PerformanceReviewReadinessDto>> Handle(GetPerformanceReviewReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = PerformanceReviewGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PerformanceReviewReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<PerformanceReviewReadinessDto>.Fail("PerformanceReview readiness record was not found.", 404)
            : Response<PerformanceReviewReadinessDto>.Success(PerformanceReviewMapper.ToDto(entity));
    }
}

public sealed class GetPerformanceReviewAuditMetadataHandler
    : IRequestHandler<GetPerformanceReviewAuditMetadataQuery, Response<PerformanceReviewAuditMetadataDto>>
{
    private readonly IPerformanceReviewReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetPerformanceReviewAuditMetadataHandler(IPerformanceReviewReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<PerformanceReviewAuditMetadataDto>> Handle(GetPerformanceReviewAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = PerformanceReviewGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PerformanceReviewAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<PerformanceReviewAuditMetadataDto>.Fail("PerformanceReview readiness record was not found.", 404)
            : Response<PerformanceReviewAuditMetadataDto>.Success(PerformanceReviewMapper.ToAuditMetadata(entity));
    }
}
