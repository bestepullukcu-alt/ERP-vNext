using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Handlers;

public sealed class DeleteTimeAttendanceLeaveReadinessHandler : IRequestHandler<DeleteTimeAttendanceLeaveReadinessCommand, Response<bool>>
{
    private readonly ITimeAttendanceLeaveReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteTimeAttendanceLeaveReadinessHandler(ITimeAttendanceLeaveReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteTimeAttendanceLeaveReadinessCommand request, CancellationToken ct)
    {
        var tenant = TimeAttendanceLeaveGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("TimeAttendanceLeave readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.TimeAttendanceLeaveReadinessState = TimeAttendanceLeaveReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
