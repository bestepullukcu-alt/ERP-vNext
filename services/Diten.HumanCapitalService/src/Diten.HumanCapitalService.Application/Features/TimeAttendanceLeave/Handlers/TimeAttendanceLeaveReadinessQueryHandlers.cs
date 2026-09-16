using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Handlers;

public sealed class GetTimeAttendanceLeaveReadinessListHandler
    : IRequestHandler<GetTimeAttendanceLeaveReadinessListQuery, Response<IReadOnlyList<TimeAttendanceLeaveReadinessListItemDto>>>
{
    private readonly ITimeAttendanceLeaveReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTimeAttendanceLeaveReadinessListHandler(ITimeAttendanceLeaveReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<TimeAttendanceLeaveReadinessListItemDto>>> Handle(
        GetTimeAttendanceLeaveReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = TimeAttendanceLeaveGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<TimeAttendanceLeaveReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<TimeAttendanceLeaveReadinessListItemDto>>.Success(rows.Select(TimeAttendanceLeaveMapper.ToListItem).ToList());
    }
}

public sealed class GetTimeAttendanceLeaveReadinessByIdHandler
    : IRequestHandler<GetTimeAttendanceLeaveReadinessByIdQuery, Response<TimeAttendanceLeaveReadinessDto>>
{
    private readonly ITimeAttendanceLeaveReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTimeAttendanceLeaveReadinessByIdHandler(ITimeAttendanceLeaveReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TimeAttendanceLeaveReadinessDto>> Handle(GetTimeAttendanceLeaveReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = TimeAttendanceLeaveGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TimeAttendanceLeaveReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<TimeAttendanceLeaveReadinessDto>.Fail("TimeAttendanceLeave readiness record was not found.", 404)
            : Response<TimeAttendanceLeaveReadinessDto>.Success(TimeAttendanceLeaveMapper.ToDto(entity));
    }
}

public sealed class GetTimeAttendanceLeaveAuditMetadataHandler
    : IRequestHandler<GetTimeAttendanceLeaveAuditMetadataQuery, Response<TimeAttendanceLeaveAuditMetadataDto>>
{
    private readonly ITimeAttendanceLeaveReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTimeAttendanceLeaveAuditMetadataHandler(ITimeAttendanceLeaveReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TimeAttendanceLeaveAuditMetadataDto>> Handle(GetTimeAttendanceLeaveAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = TimeAttendanceLeaveGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TimeAttendanceLeaveAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<TimeAttendanceLeaveAuditMetadataDto>.Fail("TimeAttendanceLeave readiness record was not found.", 404)
            : Response<TimeAttendanceLeaveAuditMetadataDto>.Success(TimeAttendanceLeaveMapper.ToAuditMetadata(entity));
    }
}
