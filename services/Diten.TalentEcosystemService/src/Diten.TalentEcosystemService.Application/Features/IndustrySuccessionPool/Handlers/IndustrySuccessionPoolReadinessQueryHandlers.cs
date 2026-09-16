using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Handlers;

public sealed class GetIndustrySuccessionPoolReadinessListHandler
    : IRequestHandler<GetIndustrySuccessionPoolReadinessListQuery, Response<IReadOnlyList<IndustrySuccessionPoolReadinessListItemDto>>>
{
    private readonly IIndustrySuccessionPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetIndustrySuccessionPoolReadinessListHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<IndustrySuccessionPoolReadinessListItemDto>>> Handle(
        GetIndustrySuccessionPoolReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<IndustrySuccessionPoolReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<IndustrySuccessionPoolReadinessListItemDto>>.Success(rows.Select(IndustrySuccessionPoolMapper.ToListItem).ToList());
    }
}

public sealed class GetIndustrySuccessionPoolReadinessByIdHandler
    : IRequestHandler<GetIndustrySuccessionPoolReadinessByIdQuery, Response<IndustrySuccessionPoolReadinessDto>>
{
    private readonly IIndustrySuccessionPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetIndustrySuccessionPoolReadinessByIdHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IndustrySuccessionPoolReadinessDto>> Handle(GetIndustrySuccessionPoolReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySuccessionPoolReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<IndustrySuccessionPoolReadinessDto>.Fail("IndustrySuccessionPool readiness record was not found.", 404)
            : Response<IndustrySuccessionPoolReadinessDto>.Success(IndustrySuccessionPoolMapper.ToDto(entity));
    }
}

public sealed class GetIndustrySuccessionPoolAuditMetadataHandler
    : IRequestHandler<GetIndustrySuccessionPoolAuditMetadataQuery, Response<IndustrySuccessionPoolAuditMetadataDto>>
{
    private readonly IIndustrySuccessionPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetIndustrySuccessionPoolAuditMetadataHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IndustrySuccessionPoolAuditMetadataDto>> Handle(GetIndustrySuccessionPoolAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySuccessionPoolAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<IndustrySuccessionPoolAuditMetadataDto>.Fail("IndustrySuccessionPool readiness record was not found.", 404)
            : Response<IndustrySuccessionPoolAuditMetadataDto>.Success(IndustrySuccessionPoolMapper.ToAuditMetadata(entity));
    }
}
