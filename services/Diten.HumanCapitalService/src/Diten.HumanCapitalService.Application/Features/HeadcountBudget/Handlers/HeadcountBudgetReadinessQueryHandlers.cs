using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Handlers;

public sealed class GetHeadcountBudgetReadinessListHandler
    : IRequestHandler<GetHeadcountBudgetReadinessListQuery, Response<IReadOnlyList<HeadcountBudgetReadinessListItemDto>>>
{
    private readonly IHeadcountBudgetReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHeadcountBudgetReadinessListHandler(IHeadcountBudgetReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<HeadcountBudgetReadinessListItemDto>>> Handle(
        GetHeadcountBudgetReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = HeadcountBudgetGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<HeadcountBudgetReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<HeadcountBudgetReadinessListItemDto>>.Success(rows.Select(HeadcountBudgetMapper.ToListItem).ToList());
    }
}

public sealed class GetHeadcountBudgetReadinessByIdHandler
    : IRequestHandler<GetHeadcountBudgetReadinessByIdQuery, Response<HeadcountBudgetReadinessDto>>
{
    private readonly IHeadcountBudgetReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHeadcountBudgetReadinessByIdHandler(IHeadcountBudgetReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HeadcountBudgetReadinessDto>> Handle(GetHeadcountBudgetReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = HeadcountBudgetGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HeadcountBudgetReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<HeadcountBudgetReadinessDto>.Fail("HeadcountBudget readiness record was not found.", 404)
            : Response<HeadcountBudgetReadinessDto>.Success(HeadcountBudgetMapper.ToDto(entity));
    }
}

public sealed class GetHeadcountBudgetAuditMetadataHandler
    : IRequestHandler<GetHeadcountBudgetAuditMetadataQuery, Response<HeadcountBudgetAuditMetadataDto>>
{
    private readonly IHeadcountBudgetReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHeadcountBudgetAuditMetadataHandler(IHeadcountBudgetReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HeadcountBudgetAuditMetadataDto>> Handle(GetHeadcountBudgetAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = HeadcountBudgetGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HeadcountBudgetAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<HeadcountBudgetAuditMetadataDto>.Fail("HeadcountBudget readiness record was not found.", 404)
            : Response<HeadcountBudgetAuditMetadataDto>.Success(HeadcountBudgetMapper.ToAuditMetadata(entity));
    }
}
