using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Handlers;

public sealed class GetTalentDataFoundationReadinessListHandler
    : IRequestHandler<GetTalentDataFoundationReadinessListQuery, Response<IReadOnlyList<TalentDataFoundationReadinessListItemDto>>>
{
    private readonly ITalentDataFoundationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTalentDataFoundationReadinessListHandler(ITalentDataFoundationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<TalentDataFoundationReadinessListItemDto>>> Handle(
        GetTalentDataFoundationReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = TalentDataFoundationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<TalentDataFoundationReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<TalentDataFoundationReadinessListItemDto>>.Success(rows.Select(TalentDataFoundationMapper.ToListItem).ToList());
    }
}

public sealed class GetTalentDataFoundationReadinessByIdHandler
    : IRequestHandler<GetTalentDataFoundationReadinessByIdQuery, Response<TalentDataFoundationReadinessDto>>
{
    private readonly ITalentDataFoundationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTalentDataFoundationReadinessByIdHandler(ITalentDataFoundationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TalentDataFoundationReadinessDto>> Handle(GetTalentDataFoundationReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = TalentDataFoundationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentDataFoundationReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<TalentDataFoundationReadinessDto>.Fail("TalentDataFoundation readiness record was not found.", 404)
            : Response<TalentDataFoundationReadinessDto>.Success(TalentDataFoundationMapper.ToDto(entity));
    }
}

public sealed class GetTalentDataFoundationAuditMetadataHandler
    : IRequestHandler<GetTalentDataFoundationAuditMetadataQuery, Response<TalentDataFoundationAuditMetadataDto>>
{
    private readonly ITalentDataFoundationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTalentDataFoundationAuditMetadataHandler(ITalentDataFoundationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TalentDataFoundationAuditMetadataDto>> Handle(GetTalentDataFoundationAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = TalentDataFoundationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentDataFoundationAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<TalentDataFoundationAuditMetadataDto>.Fail("TalentDataFoundation readiness record was not found.", 404)
            : Response<TalentDataFoundationAuditMetadataDto>.Success(TalentDataFoundationMapper.ToAuditMetadata(entity));
    }
}
