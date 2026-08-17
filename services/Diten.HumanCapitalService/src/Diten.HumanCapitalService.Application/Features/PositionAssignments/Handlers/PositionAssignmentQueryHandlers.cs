using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Queries;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Handlers;

public sealed class GetPositionAssignmentListHandler
    : IRequestHandler<GetPositionAssignmentListQuery, Response<IReadOnlyList<PositionAssignmentListItemDto>>>
{
    private readonly IPositionAssignmentOverlayRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public GetPositionAssignmentListHandler(
        IPositionAssignmentOverlayRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<PositionAssignmentListItemDto>>> Handle(
        GetPositionAssignmentListQuery request,
        CancellationToken ct)
    {
        var tenant = PositionAssignmentGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<PositionAssignmentListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        var visibleRows = new List<PositionAssignmentListItemDto>();
        foreach (var row in rows)
        {
            var employee = await _employeeRepository.GetByIdAsync(tenant.Data, row.EmployeeProjectionId, ct);
            if (employee is null)
            {
                continue;
            }

            var sensitive = await PositionAssignmentGuard.EvaluateSensitiveAccessAsync(tenant.Data, employee, _dataScopeEvaluator, ct);
            if (PositionAssignmentGuard.IsBroadReadAllowed(employee, sensitive))
            {
                visibleRows.Add(PositionAssignmentMapper.ToListItem(row));
            }
        }

        return Response<IReadOnlyList<PositionAssignmentListItemDto>>.Success(
            visibleRows);
    }
}

public sealed class GetPositionAssignmentByIdHandler
    : IRequestHandler<GetPositionAssignmentByIdQuery, Response<PositionAssignmentDto>>
{
    private readonly IPositionAssignmentOverlayRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public GetPositionAssignmentByIdHandler(
        IPositionAssignmentOverlayRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
    }

    public async Task<Response<PositionAssignmentDto>> Handle(GetPositionAssignmentByIdQuery request, CancellationToken ct)
    {
        var tenant = PositionAssignmentGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PositionAssignmentDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var assignment = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (assignment is null)
        {
            return Response<PositionAssignmentDto>.Fail("Position assignment overlay was not found.", 404);
        }

        var employee = await _employeeRepository.GetByIdAsync(tenant.Data, assignment.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<PositionAssignmentDto>.Fail("Employee projection anchor was not found.", 404);
        }

        var sensitive = await PositionAssignmentGuard.EvaluateSensitiveAccessAsync(tenant.Data, employee, _dataScopeEvaluator, ct);
        if (!PositionAssignmentGuard.IsBroadReadAllowed(employee, sensitive))
        {
            return Response<PositionAssignmentDto>.Fail("Sensitive access precondition denied assignment read.", 403);
        }

        return Response<PositionAssignmentDto>.Success(PositionAssignmentMapper.ToDto(assignment));
    }
}

public sealed class GetPositionAssignmentHealthHandler
    : IRequestHandler<GetPositionAssignmentHealthQuery, Response<PositionAssignmentHealthDto>>
{
    public Task<Response<PositionAssignmentHealthDto>> Handle(GetPositionAssignmentHealthQuery request, CancellationToken ct) =>
        Task.FromResult(Response<PositionAssignmentHealthDto>.Success(
            new PositionAssignmentHealthDto(PositionAssignmentGuard.OwnerKey, "Healthy")));
}
