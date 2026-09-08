using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Handlers;

public sealed class EvaluateTimeAttendanceLeaveReadinessHandler : IRequestHandler<EvaluateTimeAttendanceLeaveReadinessCommand, Response<TimeAttendanceLeaveReadinessDto>>
{
    private readonly ITimeAttendanceLeaveReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateTimeAttendanceLeaveReadinessHandler(ITimeAttendanceLeaveReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TimeAttendanceLeaveReadinessDto>> Handle(EvaluateTimeAttendanceLeaveReadinessCommand request, CancellationToken ct)
    {
        var tenant = TimeAttendanceLeaveGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TimeAttendanceLeaveReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<TimeAttendanceLeaveReadinessDto>.Fail("TimeAttendanceLeave readiness record was not found.", 404);
        }

        TimeAttendanceLeaveGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<TimeAttendanceLeaveReadinessDto>.Success(TimeAttendanceLeaveMapper.ToDto(entity));
    }
}
