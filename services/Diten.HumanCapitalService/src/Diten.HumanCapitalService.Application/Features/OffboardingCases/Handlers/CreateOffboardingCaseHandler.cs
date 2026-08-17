using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Handlers;

public sealed class CreateOffboardingCaseHandler
    : IRequestHandler<CreateOffboardingCaseCommand, Response<Guid>>
{
    private readonly IOffboardingCaseRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly IPositionAssignmentOverlayRepository _assignmentRepository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public CreateOffboardingCaseHandler(
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

    public async Task<Response<Guid>> Handle(CreateOffboardingCaseCommand request, CancellationToken ct)
    {
        var tenant = OffboardingCaseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = OffboardingCaseGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = OffboardingCaseGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active offboarding case with the same Code already exists for this tenant.", 409);
        }

        var employee = await OffboardingCaseGuard.ValidateEmployeeAnchorAsync(
            tenantId,
            request.Request.EmployeeProjectionId,
            _employeeRepository,
            ct);
        if (!employee.IsSuccessful)
        {
            return Response<Guid>.Fail(employee.Errors, employee.StatusCode);
        }

        var sensitive = await OffboardingCaseGuard.EvaluateSensitiveAccessAsync(tenantId, employee.Data!, _dataScopeEvaluator, ct);
        if (!OffboardingCaseGuard.IsSensitiveAccessAllowedForMutation(sensitive))
        {
            return Response<Guid>.Fail("Sensitive access precondition did not allow offboarding mutation.", 403);
        }

        var assignment = await OffboardingCaseGuard.ValidateAssignmentContextAsync(
            tenantId,
            request.Request.AssignmentOverlayId,
            _assignmentRepository,
            ct);
        if (!assignment.IsSuccessful)
        {
            return Response<Guid>.Fail(assignment.Errors, assignment.StatusCode);
        }

        var assignmentDecision = assignment.Data!;
        var now = DateTimeOffset.UtcNow;
        var entity = new OffboardingCase
        {
            TenantId = tenantId,
            Code = code,
            EmployeeProjectionId = request.Request.EmployeeProjectionId,
            AssignmentOverlayId = request.Request.AssignmentOverlayId,
            ExitReasonCode = request.Request.ExitReasonCode.Trim(),
            ExitTypeCode = request.Request.ExitTypeCode.Trim(),
            NoticeDate = request.Request.NoticeDate,
            PlannedExitDate = request.Request.PlannedExitDate,
            ActualExitDate = request.Request.ActualExitDate,
            OffboardingState = ResolveOffboardingState(request.Request.OffboardingState, assignmentDecision),
            ChecklistState = request.Request.ChecklistState,
            SensitiveAccessDecisionState = sensitive,
            DependencyDecisionState = request.Request.DependencyDecisionState,
            TepHandoffState = request.Request.TepHandoffState,
            TepHandoffReferenceKey = NormalizeOptional(request.Request.TepHandoffReferenceKey),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            OffboardingVersion = request.Request.OffboardingVersion,
            LastDependencyEvaluatedAt = now,
            LastHandoffPlannedAt = IsHandoffPlanned(request.Request.TepHandoffState) ? now : null,
            DeferredReason = OffboardingCaseGuard.MergeDeferredReason(
                assignmentDecision.DeferredReason,
                request.Request.DependencyDecisionState == OffboardingDependencyDecisionState.Deferred ? "Workflow, audit, evidence, or retention dependency metadata is deferred." : null,
                request.Request.TepHandoffState == OffboardingTepHandoffState.Deferred ? "TEP handoff metadata is deferred." : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private static OffboardingState ResolveOffboardingState(
        OffboardingState requested,
        AssignmentContextDecision assignmentDecision) =>
        !assignmentDecision.IsValidated && requested is OffboardingState.Approved or OffboardingState.Active or OffboardingState.TepHandoffReady or OffboardingState.Completed
            ? OffboardingState.ReviewRequired
            : requested;

    private static bool IsHandoffPlanned(OffboardingTepHandoffState state) =>
        state is OffboardingTepHandoffState.Planned or OffboardingTepHandoffState.Ready or OffboardingTepHandoffState.Deferred;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
