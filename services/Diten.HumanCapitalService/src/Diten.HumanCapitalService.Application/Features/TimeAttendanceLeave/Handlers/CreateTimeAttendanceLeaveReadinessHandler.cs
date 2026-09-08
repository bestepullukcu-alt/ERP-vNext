using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Handlers;

public sealed class CreateTimeAttendanceLeaveReadinessHandler : IRequestHandler<CreateTimeAttendanceLeaveReadinessCommand, Response<Guid>>
{
    private readonly ITimeAttendanceLeaveReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateTimeAttendanceLeaveReadinessHandler(ITimeAttendanceLeaveReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateTimeAttendanceLeaveReadinessCommand request, CancellationToken ct)
    {
        var tenant = TimeAttendanceLeaveGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = TimeAttendanceLeaveGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = TimeAttendanceLeaveGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active time-attendance-leave readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = TimeAttendanceLeaveGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new TimeAttendanceLeaveReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            TimeAttendanceLeaveReadinessState = readinessState,
            TimesheetIntakeBoundaryState = request.Request.TimesheetIntakeBoundaryState,
            AttendanceSyncBoundaryState = request.Request.AttendanceSyncBoundaryState,
            LeaveRequestBoundaryState = request.Request.LeaveRequestBoundaryState,
            LeaveBalanceBoundaryState = request.Request.LeaveBalanceBoundaryState,
            ScheduleConsumptionBoundaryState = request.Request.ScheduleConsumptionBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TimeAttendanceSourceDependencyState = request.Request.TimeAttendanceSourceDependencyState,
            LeaveSourceDependencyState = request.Request.LeaveSourceDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, TimeAttendanceLeaveReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            TimeAttendanceLeaveReadinessVersion = request.Request.TimeAttendanceLeaveReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = TimeAttendanceLeaveGuard.MergeDeferredReason(
                TimeAttendanceLeaveGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == TimeAttendanceLeaveReadinessState.Deferred
                    ? "Time, attendance and leave readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
