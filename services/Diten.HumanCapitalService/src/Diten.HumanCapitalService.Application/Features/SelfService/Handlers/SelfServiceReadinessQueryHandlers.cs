using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SelfService.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SelfService.Handlers;

public sealed class GetSelfServiceReadinessListHandler
    : IRequestHandler<GetSelfServiceReadinessListQuery, Response<IReadOnlyList<SelfServiceReadinessListItemDto>>>
{
    private readonly ISelfServiceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSelfServiceReadinessListHandler(ISelfServiceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<SelfServiceReadinessListItemDto>>> Handle(
        GetSelfServiceReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = SelfServiceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<SelfServiceReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<SelfServiceReadinessListItemDto>>.Success(rows.Select(SelfServiceMapper.ToListItem).ToList());
    }
}

public sealed class GetSelfServiceReadinessByIdHandler
    : IRequestHandler<GetSelfServiceReadinessByIdQuery, Response<SelfServiceReadinessDto>>
{
    private readonly ISelfServiceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSelfServiceReadinessByIdHandler(ISelfServiceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SelfServiceReadinessDto>> Handle(GetSelfServiceReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = SelfServiceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SelfServiceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<SelfServiceReadinessDto>.Fail("SelfService readiness record was not found.", 404)
            : Response<SelfServiceReadinessDto>.Success(SelfServiceMapper.ToDto(entity));
    }
}

public sealed class GetSelfServiceAuditMetadataHandler
    : IRequestHandler<GetSelfServiceAuditMetadataQuery, Response<SelfServiceAuditMetadataDto>>
{
    private readonly ISelfServiceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSelfServiceAuditMetadataHandler(ISelfServiceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SelfServiceAuditMetadataDto>> Handle(GetSelfServiceAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = SelfServiceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SelfServiceAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<SelfServiceAuditMetadataDto>.Fail("SelfService readiness record was not found.", 404)
            : Response<SelfServiceAuditMetadataDto>.Success(SelfServiceMapper.ToAuditMetadata(entity));
    }
}
