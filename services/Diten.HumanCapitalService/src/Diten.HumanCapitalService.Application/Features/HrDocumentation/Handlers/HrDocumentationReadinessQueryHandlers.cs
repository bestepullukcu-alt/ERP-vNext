using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Handlers;

public sealed class GetHrDocumentationReadinessListHandler
    : IRequestHandler<GetHrDocumentationReadinessListQuery, Response<IReadOnlyList<HrDocumentationReadinessListItemDto>>>
{
    private readonly IHrDocumentationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHrDocumentationReadinessListHandler(IHrDocumentationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<HrDocumentationReadinessListItemDto>>> Handle(
        GetHrDocumentationReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = HrDocumentationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<HrDocumentationReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<HrDocumentationReadinessListItemDto>>.Success(rows.Select(HrDocumentationMapper.ToListItem).ToList());
    }
}

public sealed class GetHrDocumentationReadinessByIdHandler
    : IRequestHandler<GetHrDocumentationReadinessByIdQuery, Response<HrDocumentationReadinessDto>>
{
    private readonly IHrDocumentationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHrDocumentationReadinessByIdHandler(IHrDocumentationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HrDocumentationReadinessDto>> Handle(GetHrDocumentationReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = HrDocumentationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrDocumentationReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<HrDocumentationReadinessDto>.Fail("HrDocumentation readiness record was not found.", 404)
            : Response<HrDocumentationReadinessDto>.Success(HrDocumentationMapper.ToDto(entity));
    }
}

public sealed class GetHrDocumentationAuditMetadataHandler
    : IRequestHandler<GetHrDocumentationAuditMetadataQuery, Response<HrDocumentationAuditMetadataDto>>
{
    private readonly IHrDocumentationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetHrDocumentationAuditMetadataHandler(IHrDocumentationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HrDocumentationAuditMetadataDto>> Handle(GetHrDocumentationAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = HrDocumentationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrDocumentationAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<HrDocumentationAuditMetadataDto>.Fail("HrDocumentation readiness record was not found.", 404)
            : Response<HrDocumentationAuditMetadataDto>.Success(HrDocumentationMapper.ToAuditMetadata(entity));
    }
}
