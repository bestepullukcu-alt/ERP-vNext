using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Handlers;

public sealed class GetEmploymentChangeReadinessListHandler
    : IRequestHandler<GetEmploymentChangeReadinessListQuery, Response<IReadOnlyList<EmploymentChangeReadinessListItemDto>>>
{
    private readonly IEmploymentChangeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmploymentChangeReadinessListHandler(IEmploymentChangeReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<EmploymentChangeReadinessListItemDto>>> Handle(
        GetEmploymentChangeReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = EmploymentChangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<EmploymentChangeReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<EmploymentChangeReadinessListItemDto>>.Success(rows.Select(EmploymentChangeMapper.ToListItem).ToList());
    }
}

public sealed class GetEmploymentChangeReadinessByIdHandler
    : IRequestHandler<GetEmploymentChangeReadinessByIdQuery, Response<EmploymentChangeReadinessDto>>
{
    private readonly IEmploymentChangeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmploymentChangeReadinessByIdHandler(IEmploymentChangeReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<EmploymentChangeReadinessDto>> Handle(GetEmploymentChangeReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = EmploymentChangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EmploymentChangeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<EmploymentChangeReadinessDto>.Fail("EmploymentChange readiness record was not found.", 404)
            : Response<EmploymentChangeReadinessDto>.Success(EmploymentChangeMapper.ToDto(entity));
    }
}

public sealed class GetEmploymentChangeAuditMetadataHandler
    : IRequestHandler<GetEmploymentChangeAuditMetadataQuery, Response<EmploymentChangeAuditMetadataDto>>
{
    private readonly IEmploymentChangeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmploymentChangeAuditMetadataHandler(IEmploymentChangeReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<EmploymentChangeAuditMetadataDto>> Handle(GetEmploymentChangeAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = EmploymentChangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EmploymentChangeAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<EmploymentChangeAuditMetadataDto>.Fail("EmploymentChange readiness record was not found.", 404)
            : Response<EmploymentChangeAuditMetadataDto>.Success(EmploymentChangeMapper.ToAuditMetadata(entity));
    }
}
