using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Handlers;

public sealed class UpdateOffboardingCaseHandler
    : IRequestHandler<UpdateOffboardingCaseCommand, Response<OffboardingCaseDto>>
{
    private readonly IOffboardingCaseRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly IPositionAssignmentOverlayRepository _assignmentRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public UpdateOffboardingCaseHandler(
        IOffboardingCaseRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        IPositionAssignmentOverlayRepository assignmentRepository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _assignmentRepository = assignmentRepository;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
    }

    public async Task<Response<OffboardingCaseDto>> Handle(UpdateOffboardingCaseCommand request, CancellationToken ct)
    {
        var tenant = OffboardingCaseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<OffboardingCaseDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = OffboardingCaseGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<OffboardingCaseDto>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<OffboardingCaseDto>.Fail("Offboarding case was not found.", 404);
        }

        var code = OffboardingCaseGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, request.Id, ct))
        {
            return Response<OffboardingCaseDto>.Fail("An active offboarding case with the same Code already exists for this tenant.", 409);
        }

        var employee = await OffboardingCaseGuard.ValidateEmployeeAnchorAsync(
            tenantId,
            request.Request.EmployeeProjectionId,
            _employeeRepository,
            ct);
        if (!employee.IsSuccessful)
        {
            return Response<OffboardingCaseDto>.Fail(employee.Errors, employee.StatusCode);
        }

        var sensitive = await OffboardingCaseGuard.EvaluateSensitiveAccessAsync(tenantId, employee.Data!, _dataScopeEvaluator, ct);
        if (!OffboardingCaseGuard.IsSensitiveAccessAllowedForMutation(sensitive))
        {
            return Response<OffboardingCaseDto>.Fail("Sensitive access precondition did not allow offboarding mutation.", 403);
        }

        var assignment = await OffboardingCaseGuard.ValidateAssignmentContextAsync(
            tenantId,
            request.Request.AssignmentOverlayId,
            _assignmentRepository,
            ct);
        if (!assignment.IsSuccessful)
        {
            return Response<OffboardingCaseDto>.Fail(assignment.Errors, assignment.StatusCode);
        }

        var assignmentDecision = assignment.Data!;
        var now = DateTimeOffset.UtcNow;
        entity.Code = code;
        entity.EmployeeProjectionId = request.Request.EmployeeProjectionId;
        entity.AssignmentOverlayId = request.Request.AssignmentOverlayId;
        entity.ExitReasonCode = request.Request.ExitReasonCode.Trim();
        entity.ExitTypeCode = request.Request.ExitTypeCode.Trim();
        entity.NoticeDate = request.Request.NoticeDate;
        entity.PlannedExitDate = request.Request.PlannedExitDate;
        entity.ActualExitDate = request.Request.ActualExitDate;
        entity.OffboardingState = !assignmentDecision.IsValidated && request.Request.OffboardingState is OffboardingState.Approved or OffboardingState.Active or OffboardingState.TepHandoffReady or OffboardingState.Completed
            ? OffboardingState.ReviewRequired
            : request.Request.OffboardingState;
        entity.ChecklistState = request.Request.ChecklistState;
        entity.SensitiveAccessDecisionState = sensitive;
        entity.DependencyDecisionState = request.Request.DependencyDecisionState;
        entity.TepHandoffState = request.Request.TepHandoffState;
        entity.TepHandoffReferenceKey = string.IsNullOrWhiteSpace(request.Request.TepHandoffReferenceKey) ? null : request.Request.TepHandoffReferenceKey.Trim();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.OffboardingVersion = request.Request.OffboardingVersion;
        entity.LastDependencyEvaluatedAt = now;
        entity.LastHandoffPlannedAt = request.Request.TepHandoffState is OffboardingTepHandoffState.Planned or OffboardingTepHandoffState.Ready or OffboardingTepHandoffState.Deferred ? now : entity.LastHandoffPlannedAt;
        entity.DeferredReason = OffboardingCaseGuard.MergeDeferredReason(
            assignmentDecision.DeferredReason,
            request.Request.DependencyDecisionState == OffboardingDependencyDecisionState.Deferred ? "Workflow, audit, evidence, or retention dependency metadata is deferred." : null,
            request.Request.TepHandoffState == OffboardingTepHandoffState.Deferred ? "TEP handoff metadata is deferred." : null);
        entity.UpdatedAt = now;

        await _repository.UpdateAsync(entity, ct);
        return Response<OffboardingCaseDto>.Success(OffboardingCaseMapper.ToDto(entity));
    }
}
