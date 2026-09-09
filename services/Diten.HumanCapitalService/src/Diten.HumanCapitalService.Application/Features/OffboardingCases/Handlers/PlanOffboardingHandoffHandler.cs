using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Handlers;

public sealed class PlanOffboardingHandoffHandler
    : IRequestHandler<PlanOffboardingHandoffCommand, Response<OffboardingCaseDto>>
{
    private readonly IOffboardingCaseRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public PlanOffboardingHandoffHandler(
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

    public async Task<Response<OffboardingCaseDto>> Handle(PlanOffboardingHandoffCommand request, CancellationToken ct)
    {
        var tenant = OffboardingCaseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<OffboardingCaseDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = OffboardingCaseGuard.ValidateHandoff(request.Request);
        if (errors.Count > 0)
        {
            return Response<OffboardingCaseDto>.Fail(errors, 400);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<OffboardingCaseDto>.Fail("Offboarding case was not found.", 404);
        }

        var employee = await _employeeRepository.GetByIdAsync(tenant.Data, scope, entity.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<OffboardingCaseDto>.Fail("Employee projection anchor was not found.", 404);
        }

        var sensitive = await OffboardingCaseGuard.EvaluateSensitiveAccessAsync(tenant.Data, employee, _dataScopeEvaluator, ct);
        if (!OffboardingCaseGuard.IsSensitiveAccessAllowedForMutation(sensitive))
        {
            return Response<OffboardingCaseDto>.Fail("Sensitive access precondition did not allow offboarding handoff planning.", 403);
        }

        var now = DateTimeOffset.UtcNow;
        entity.TepHandoffState = request.Request.TepHandoffState;
        entity.TepHandoffReferenceKey = string.IsNullOrWhiteSpace(request.Request.TepHandoffReferenceKey) ? null : request.Request.TepHandoffReferenceKey.Trim();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.OffboardingVersion = request.Request.OffboardingVersion;
        entity.SensitiveAccessDecisionState = sensitive;
        entity.LastHandoffPlannedAt = now;
        entity.DeferredReason = request.Request.TepHandoffState == OffboardingTepHandoffState.Deferred
            ? OffboardingCaseGuard.MergeDeferredReason(entity.DeferredReason, "TEP handoff metadata is deferred.")
            : entity.DeferredReason;
        entity.UpdatedAt = now;

        await _repository.UpdateAsync(entity, ct);
        return Response<OffboardingCaseDto>.Success(OffboardingCaseMapper.ToDto(entity));
    }
}
