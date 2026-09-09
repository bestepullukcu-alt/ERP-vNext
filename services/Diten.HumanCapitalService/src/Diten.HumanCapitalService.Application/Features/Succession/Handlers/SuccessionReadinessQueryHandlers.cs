using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.Succession.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Handlers;

public sealed class GetSuccessionReadinessListHandler
    : IRequestHandler<GetSuccessionReadinessListQuery, Response<IReadOnlyList<SuccessionReadinessListItemDto>>>
{
    private readonly ISuccessionReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetSuccessionReadinessListHandler(ISuccessionReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<SuccessionReadinessListItemDto>>> Handle(
        GetSuccessionReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = SuccessionGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<SuccessionReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<SuccessionReadinessListItemDto>>.Success(rows.Select(SuccessionMapper.ToListItem).ToList());
    }
}

public sealed class GetSuccessionReadinessByIdHandler
    : IRequestHandler<GetSuccessionReadinessByIdQuery, Response<SuccessionReadinessDto>>
{
    private readonly ISuccessionReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetSuccessionReadinessByIdHandler(ISuccessionReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SuccessionReadinessDto>> Handle(GetSuccessionReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = SuccessionGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SuccessionReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<SuccessionReadinessDto>.Fail("Succession readiness record was not found.", 404)
            : Response<SuccessionReadinessDto>.Success(SuccessionMapper.ToDto(entity));
    }
}

public sealed class GetSuccessionAuditMetadataHandler
    : IRequestHandler<GetSuccessionAuditMetadataQuery, Response<SuccessionAuditMetadataDto>>
{
    private readonly ISuccessionReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetSuccessionAuditMetadataHandler(ISuccessionReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SuccessionAuditMetadataDto>> Handle(GetSuccessionAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = SuccessionGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SuccessionAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<SuccessionAuditMetadataDto>.Fail("Succession readiness record was not found.", 404)
            : Response<SuccessionAuditMetadataDto>.Success(SuccessionMapper.ToAuditMetadata(entity));
    }
}
