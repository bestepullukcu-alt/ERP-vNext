using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess.Handlers;

public sealed class EvaluateEmployeeProjectionSensitiveAccessHandler
    : IRequestHandler<EvaluateEmployeeProjectionSensitiveAccessQuery, Response<SensitiveAccessDecisionDto>>
{
    private const string LocalBoundedAuditMode = "LocalBoundedDeferred";

    private readonly IEmployeeProjectionRepository _repository;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateEmployeeProjectionSensitiveAccessHandler(
        IEmployeeProjectionRepository repository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SensitiveAccessDecisionDto>> Handle(
        EvaluateEmployeeProjectionSensitiveAccessQuery request,
        CancellationToken ct)
    {
        var versionErrors = SensitiveAccessGuard.ValidatePolicyVersion(request.Request.SourcePolicyVersion);
        if (versionErrors.Count > 0)
        {
            return Response<SensitiveAccessDecisionDto>.Fail(versionErrors, 400);
        }

        if (_tenantContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            return Response<SensitiveAccessDecisionDto>.Fail("Tenant context is required.", 401);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var projection = await _repository.GetByIdAsync(tenantId, scope, request.EmployeeProjectionId, ct);
        if (projection is null)
        {
            return Response<SensitiveAccessDecisionDto>.Fail("Employee projection was not found.", 404);
        }

        var permissionReason = EvaluatePermission(projection.VisibilityClassification, request.Request.RequestedAction, request.EffectivePermissions);
        if (permissionReason is not null)
        {
            return Response<SensitiveAccessDecisionDto>.Success(new SensitiveAccessDecisionDto(
                projection.Id,
                projection.VisibilityClassification,
                request.Request.RequestedAction,
                SensitiveAccessDecision.Denied,
                false,
                permissionReason,
                false,
                "DataScopeNotEvaluated",
                LocalBoundedAuditMode,
                SensitiveAccessGuard.OwnerKey,
                request.Request.SourcePolicyVersion));
        }

        var dataScope = await _dataScopeEvaluator.EvaluateAsync(tenantId, projection, ct);
        if (!dataScope.IsAvailable)
        {
            return Response<SensitiveAccessDecisionDto>.Success(new SensitiveAccessDecisionDto(
                projection.Id,
                projection.VisibilityClassification,
                request.Request.RequestedAction,
                SensitiveAccessDecision.Deferred,
                false,
                "DataScopeDeferred",
                false,
                dataScope.ReasonCode,
                LocalBoundedAuditMode,
                SensitiveAccessGuard.OwnerKey,
                request.Request.SourcePolicyVersion));
        }

        if (!dataScope.IsInScope)
        {
            return Response<SensitiveAccessDecisionDto>.Success(new SensitiveAccessDecisionDto(
                projection.Id,
                projection.VisibilityClassification,
                request.Request.RequestedAction,
                SensitiveAccessDecision.Denied,
                false,
                "DataScopeDenied",
                true,
                dataScope.ReasonCode,
                LocalBoundedAuditMode,
                SensitiveAccessGuard.OwnerKey,
                request.Request.SourcePolicyVersion));
        }

        return Response<SensitiveAccessDecisionDto>.Success(new SensitiveAccessDecisionDto(
            projection.Id,
            projection.VisibilityClassification,
            request.Request.RequestedAction,
            SensitiveAccessDecision.Allowed,
            true,
            "AccessAllowed",
            true,
            dataScope.ReasonCode,
            LocalBoundedAuditMode,
            SensitiveAccessGuard.OwnerKey,
            request.Request.SourcePolicyVersion));
    }

    private static string? EvaluatePermission(
        EmployeeVisibilityClassification visibility,
        SensitiveAccessRequestedAction action,
        IReadOnlyCollection<string> permissions)
    {
        if (!SensitiveAccessGuard.HasPermission(permissions, SensitiveAccessGuard.EmployeeProjectionReadPermission))
        {
            return "MissingEmployeeProjectionReadPermission";
        }

        if (action == SensitiveAccessRequestedAction.Manage
            && !SensitiveAccessGuard.HasPermission(permissions, SensitiveAccessGuard.ManagePermission))
        {
            return "MissingSensitiveAccessManagePermission";
        }

        return visibility switch
        {
            EmployeeVisibilityClassification.StandardHr => null,
            EmployeeVisibilityClassification.SensitiveHr when SensitiveAccessGuard.HasPermission(permissions, SensitiveAccessGuard.ReadPermission) => null,
            EmployeeVisibilityClassification.SensitiveHr => "MissingSensitiveAccessReadPermission",
            EmployeeVisibilityClassification.RestrictedHr when HasRestrictedAccessPermissions(permissions) => null,
            EmployeeVisibilityClassification.RestrictedHr => "MissingRestrictedHrPermissionSet",
            _ => "UnsupportedVisibilityClassification"
        };
    }

    private static bool HasRestrictedAccessPermissions(IReadOnlyCollection<string> permissions) =>
        SensitiveAccessGuard.HasPermission(permissions, SensitiveAccessGuard.ReviewPermission)
        && SensitiveAccessGuard.HasPermission(permissions, SensitiveAccessGuard.AuditReadPermission);
}
