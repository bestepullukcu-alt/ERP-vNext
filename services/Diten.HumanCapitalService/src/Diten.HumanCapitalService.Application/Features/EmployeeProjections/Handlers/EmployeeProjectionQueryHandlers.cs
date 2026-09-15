using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Handlers;

public sealed class GetEmployeeProjectionListHandler : IRequestHandler<GetEmployeeProjectionListQuery, Response<IReadOnlyList<EmployeeProjectionListItemDto>>>
{
    private readonly IEmployeeProjectionRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmployeeProjectionListHandler(IEmployeeProjectionRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<EmployeeProjectionListItemDto>>> Handle(GetEmployeeProjectionListQuery request, CancellationToken ct)
    {
        var tenantId = EmployeeProjectionGuards.RequireTenant(_tenantContext);
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenantId, scope, ct);
        return Response<IReadOnlyList<EmployeeProjectionListItemDto>>.Success(items.Select(EmployeeProjectionMapper.ToListItemDto).ToList());
    }
}

public sealed class GetEmployeeProjectionByIdHandler : IRequestHandler<GetEmployeeProjectionByIdQuery, Response<EmployeeProjectionDto>>
{
    private readonly IEmployeeProjectionRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetEmployeeProjectionByIdHandler(IEmployeeProjectionRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<EmployeeProjectionDto>> Handle(GetEmployeeProjectionByIdQuery request, CancellationToken ct)
    {
        var tenantId = EmployeeProjectionGuards.RequireTenant(_tenantContext);
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        return item is null
            ? Response<EmployeeProjectionDto>.Fail("Employee projection was not found.", 404)
            : Response<EmployeeProjectionDto>.Success(EmployeeProjectionMapper.ToDto(item));
    }
}

public sealed class GetEmployeeProjectionHealthHandler : IRequestHandler<GetEmployeeProjectionHealthQuery, Response<EmployeeProjectionHealthDto>>
{
    public Task<Response<EmployeeProjectionHealthDto>> Handle(GetEmployeeProjectionHealthQuery request, CancellationToken ct) =>
        Task.FromResult(Response<EmployeeProjectionHealthDto>.Success(new EmployeeProjectionHealthDto("hcm.employee-projections", "Healthy")));
}
