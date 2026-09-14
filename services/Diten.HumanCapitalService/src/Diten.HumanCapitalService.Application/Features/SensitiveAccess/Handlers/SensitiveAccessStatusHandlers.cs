using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Commands;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess.Handlers;

public sealed class GetSensitiveAccessHealthHandler
    : IRequestHandler<GetSensitiveAccessHealthQuery, Response<SensitiveAccessHealthDto>>
{
    public Task<Response<SensitiveAccessHealthDto>> Handle(GetSensitiveAccessHealthQuery request, CancellationToken ct) =>
        Task.FromResult(Response<SensitiveAccessHealthDto>.Success(new SensitiveAccessHealthDto(SensitiveAccessGuard.OwnerKey, "Healthy")));
}

public sealed class GetSensitiveAccessAuditStatusHandler
    : IRequestHandler<GetSensitiveAccessAuditStatusQuery, Response<SensitiveAccessAuditStatusDto>>
{
    public Task<Response<SensitiveAccessAuditStatusDto>> Handle(GetSensitiveAccessAuditStatusQuery request, CancellationToken ct) =>
        Task.FromResult(Response<SensitiveAccessAuditStatusDto>.Success(new SensitiveAccessAuditStatusDto(
            "LocalBoundedDeferred",
            "MOD-0021 integration deferred",
            "Real audit integration remains a future follow-up.")));
}

public sealed class ValidateSensitiveAccessPolicyHandler
    : IRequestHandler<ValidateSensitiveAccessPolicyCommand, Response<SensitiveAccessPolicyValidationDto>>
{
    public Task<Response<SensitiveAccessPolicyValidationDto>> Handle(ValidateSensitiveAccessPolicyCommand request, CancellationToken ct)
    {
        var errors = SensitiveAccessGuard.ValidatePolicyVersion(request.Request.SourcePolicyVersion);
        if (errors.Count > 0)
        {
            return Task.FromResult(Response<SensitiveAccessPolicyValidationDto>.Fail(errors, 400));
        }

        return Task.FromResult(Response<SensitiveAccessPolicyValidationDto>.Success(new SensitiveAccessPolicyValidationDto(
            request.Request.SourcePolicyVersion,
            true,
            "Valid")));
    }
}
