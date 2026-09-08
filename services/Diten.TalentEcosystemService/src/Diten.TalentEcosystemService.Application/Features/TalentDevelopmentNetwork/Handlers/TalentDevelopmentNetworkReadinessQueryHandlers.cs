using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Handlers;

public sealed class GetTalentDevelopmentNetworkReadinessListHandler
    : IRequestHandler<GetTalentDevelopmentNetworkReadinessListQuery, Response<IReadOnlyList<TalentDevelopmentNetworkReadinessListItemDto>>>
{
    private readonly ITalentDevelopmentNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTalentDevelopmentNetworkReadinessListHandler(ITalentDevelopmentNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<TalentDevelopmentNetworkReadinessListItemDto>>> Handle(
        GetTalentDevelopmentNetworkReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = TalentDevelopmentNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<TalentDevelopmentNetworkReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<TalentDevelopmentNetworkReadinessListItemDto>>.Success(rows.Select(TalentDevelopmentNetworkMapper.ToListItem).ToList());
    }
}

public sealed class GetTalentDevelopmentNetworkReadinessByIdHandler
    : IRequestHandler<GetTalentDevelopmentNetworkReadinessByIdQuery, Response<TalentDevelopmentNetworkReadinessDto>>
{
    private readonly ITalentDevelopmentNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTalentDevelopmentNetworkReadinessByIdHandler(ITalentDevelopmentNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TalentDevelopmentNetworkReadinessDto>> Handle(GetTalentDevelopmentNetworkReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = TalentDevelopmentNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentDevelopmentNetworkReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<TalentDevelopmentNetworkReadinessDto>.Fail("TalentDevelopmentNetwork readiness record was not found.", 404)
            : Response<TalentDevelopmentNetworkReadinessDto>.Success(TalentDevelopmentNetworkMapper.ToDto(entity));
    }
}

public sealed class GetTalentDevelopmentNetworkAuditMetadataHandler
    : IRequestHandler<GetTalentDevelopmentNetworkAuditMetadataQuery, Response<TalentDevelopmentNetworkAuditMetadataDto>>
{
    private readonly ITalentDevelopmentNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTalentDevelopmentNetworkAuditMetadataHandler(ITalentDevelopmentNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TalentDevelopmentNetworkAuditMetadataDto>> Handle(GetTalentDevelopmentNetworkAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = TalentDevelopmentNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentDevelopmentNetworkAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<TalentDevelopmentNetworkAuditMetadataDto>.Fail("TalentDevelopmentNetwork readiness record was not found.", 404)
            : Response<TalentDevelopmentNetworkAuditMetadataDto>.Success(TalentDevelopmentNetworkMapper.ToAuditMetadata(entity));
    }
}
