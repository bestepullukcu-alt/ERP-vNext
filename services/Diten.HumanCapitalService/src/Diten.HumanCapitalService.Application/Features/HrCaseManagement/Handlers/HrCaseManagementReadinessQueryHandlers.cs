using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement.Handlers;

public sealed class GetHrCaseManagementReadinessListHandler
    : IRequestHandler<GetHrCaseManagementReadinessListQuery, Response<IReadOnlyList<HrCaseManagementReadinessListItemDto>>>
{
    private readonly IHrCaseManagementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHrCaseManagementReadinessListHandler(IHrCaseManagementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<HrCaseManagementReadinessListItemDto>>> Handle(
        GetHrCaseManagementReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = HrCaseManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<HrCaseManagementReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<HrCaseManagementReadinessListItemDto>>.Success(rows.Select(HrCaseManagementMapper.ToListItem).ToList());
    }
}

public sealed class GetHrCaseManagementReadinessByIdHandler
    : IRequestHandler<GetHrCaseManagementReadinessByIdQuery, Response<HrCaseManagementReadinessDto>>
{
    private readonly IHrCaseManagementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHrCaseManagementReadinessByIdHandler(IHrCaseManagementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HrCaseManagementReadinessDto>> Handle(GetHrCaseManagementReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = HrCaseManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrCaseManagementReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<HrCaseManagementReadinessDto>.Fail("HrCaseManagement readiness record was not found.", 404)
            : Response<HrCaseManagementReadinessDto>.Success(HrCaseManagementMapper.ToDto(entity));
    }
}

public sealed class GetHrCaseManagementAuditMetadataHandler
    : IRequestHandler<GetHrCaseManagementAuditMetadataQuery, Response<HrCaseManagementAuditMetadataDto>>
{
    private readonly IHrCaseManagementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHrCaseManagementAuditMetadataHandler(IHrCaseManagementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HrCaseManagementAuditMetadataDto>> Handle(GetHrCaseManagementAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = HrCaseManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrCaseManagementAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<HrCaseManagementAuditMetadataDto>.Fail("HrCaseManagement readiness record was not found.", 404)
            : Response<HrCaseManagementAuditMetadataDto>.Success(HrCaseManagementMapper.ToAuditMetadata(entity));
    }
}
