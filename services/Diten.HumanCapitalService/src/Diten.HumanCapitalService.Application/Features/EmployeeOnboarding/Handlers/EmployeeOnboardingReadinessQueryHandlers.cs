using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Handlers;

public sealed class GetEmployeeOnboardingReadinessListHandler
    : IRequestHandler<GetEmployeeOnboardingReadinessListQuery, Response<IReadOnlyList<EmployeeOnboardingReadinessListItemDto>>>
{
    private readonly IEmployeeOnboardingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmployeeOnboardingReadinessListHandler(IEmployeeOnboardingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<EmployeeOnboardingReadinessListItemDto>>> Handle(
        GetEmployeeOnboardingReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = EmployeeOnboardingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<EmployeeOnboardingReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<EmployeeOnboardingReadinessListItemDto>>.Success(rows.Select(EmployeeOnboardingMapper.ToListItem).ToList());
    }
}

public sealed class GetEmployeeOnboardingReadinessByIdHandler
    : IRequestHandler<GetEmployeeOnboardingReadinessByIdQuery, Response<EmployeeOnboardingReadinessDto>>
{
    private readonly IEmployeeOnboardingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmployeeOnboardingReadinessByIdHandler(IEmployeeOnboardingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<EmployeeOnboardingReadinessDto>> Handle(GetEmployeeOnboardingReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = EmployeeOnboardingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EmployeeOnboardingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<EmployeeOnboardingReadinessDto>.Fail("EmployeeOnboarding readiness record was not found.", 404)
            : Response<EmployeeOnboardingReadinessDto>.Success(EmployeeOnboardingMapper.ToDto(entity));
    }
}

public sealed class GetEmployeeOnboardingAuditMetadataHandler
    : IRequestHandler<GetEmployeeOnboardingAuditMetadataQuery, Response<EmployeeOnboardingAuditMetadataDto>>
{
    private readonly IEmployeeOnboardingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmployeeOnboardingAuditMetadataHandler(IEmployeeOnboardingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<EmployeeOnboardingAuditMetadataDto>> Handle(GetEmployeeOnboardingAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = EmployeeOnboardingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EmployeeOnboardingAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<EmployeeOnboardingAuditMetadataDto>.Fail("EmployeeOnboarding readiness record was not found.", 404)
            : Response<EmployeeOnboardingAuditMetadataDto>.Success(EmployeeOnboardingMapper.ToAuditMetadata(entity));
    }
}
