using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Handlers;

public sealed class GetOffboardingCaseListHandler
    : IRequestHandler<GetOffboardingCaseListQuery, Response<IReadOnlyList<OffboardingCaseListItemDto>>>
{
    private readonly IOffboardingCaseRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetOffboardingCaseListHandler(
        IOffboardingCaseRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<OffboardingCaseListItemDto>>> Handle(
        GetOffboardingCaseListQuery request,
        CancellationToken ct)
    {
        var tenant = OffboardingCaseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<OffboardingCaseListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        var visibleRows = new List<OffboardingCaseListItemDto>();
        foreach (var row in rows)
        {
            var employee = await _employeeRepository.GetByIdAsync(tenant.Data, scope, row.EmployeeProjectionId, ct);
            if (employee is null)
            {
                continue;
            }

            var sensitive = await OffboardingCaseGuard.EvaluateSensitiveAccessAsync(tenant.Data, employee, _dataScopeEvaluator, ct);
            if (OffboardingCaseGuard.IsBroadReadAllowed(employee, sensitive))
            {
                visibleRows.Add(OffboardingCaseMapper.ToListItem(row));
            }
        }

        return Response<IReadOnlyList<OffboardingCaseListItemDto>>.Success(visibleRows);
    }
}

public sealed class GetOffboardingCaseByIdHandler
    : IRequestHandler<GetOffboardingCaseByIdQuery, Response<OffboardingCaseDto>>
{
    private readonly IOffboardingCaseRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetOffboardingCaseByIdHandler(
        IOffboardingCaseRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<OffboardingCaseDto>> Handle(GetOffboardingCaseByIdQuery request, CancellationToken ct)
    {
        var tenant = OffboardingCaseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<OffboardingCaseDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var offboardingCase = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (offboardingCase is null)
        {
            return Response<OffboardingCaseDto>.Fail("Offboarding case was not found.", 404);
        }

        var employee = await _employeeRepository.GetByIdAsync(tenant.Data, scope, offboardingCase.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<OffboardingCaseDto>.Fail("Employee projection anchor was not found.", 404);
        }

        var sensitive = await OffboardingCaseGuard.EvaluateSensitiveAccessAsync(tenant.Data, employee, _dataScopeEvaluator, ct);
        if (!OffboardingCaseGuard.IsBroadReadAllowed(employee, sensitive))
        {
            return Response<OffboardingCaseDto>.Fail("Sensitive access precondition denied offboarding read.", 403);
        }

        return Response<OffboardingCaseDto>.Success(OffboardingCaseMapper.ToDto(offboardingCase));
    }
}

public sealed class GetOffboardingCaseHealthHandler
    : IRequestHandler<GetOffboardingCaseHealthQuery, Response<OffboardingCaseHealthDto>>
{
    public Task<Response<OffboardingCaseHealthDto>> Handle(GetOffboardingCaseHealthQuery request, CancellationToken ct) =>
        Task.FromResult(Response<OffboardingCaseHealthDto>.Success(new OffboardingCaseHealthDto(OffboardingCaseGuard.OwnerKey, "Healthy")));
}
