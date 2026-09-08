using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Handlers;

public sealed class GetEtlEltPipelinesReadinessListHandler
    : IRequestHandler<GetEtlEltPipelinesReadinessListQuery, Response<IReadOnlyList<EtlEltPipelinesReadinessListItemDto>>>
{
    private readonly IEtlEltPipelinesReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetEtlEltPipelinesReadinessListHandler(IEtlEltPipelinesReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<EtlEltPipelinesReadinessListItemDto>>> Handle(
        GetEtlEltPipelinesReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = EtlEltPipelinesGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<EtlEltPipelinesReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<EtlEltPipelinesReadinessListItemDto>>.Success(rows.Select(EtlEltPipelinesMapper.ToListItem).ToList());
    }
}

public sealed class GetEtlEltPipelinesReadinessByIdHandler
    : IRequestHandler<GetEtlEltPipelinesReadinessByIdQuery, Response<EtlEltPipelinesReadinessDto>>
{
    private readonly IEtlEltPipelinesReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetEtlEltPipelinesReadinessByIdHandler(IEtlEltPipelinesReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EtlEltPipelinesReadinessDto>> Handle(GetEtlEltPipelinesReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = EtlEltPipelinesGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EtlEltPipelinesReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<EtlEltPipelinesReadinessDto>.Fail("EtlEltPipelines readiness record was not found.", 404)
            : Response<EtlEltPipelinesReadinessDto>.Success(EtlEltPipelinesMapper.ToDto(entity));
    }
}

public sealed class GetEtlEltPipelinesAuditMetadataHandler
    : IRequestHandler<GetEtlEltPipelinesAuditMetadataQuery, Response<EtlEltPipelinesAuditMetadataDto>>
{
    private readonly IEtlEltPipelinesReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetEtlEltPipelinesAuditMetadataHandler(IEtlEltPipelinesReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EtlEltPipelinesAuditMetadataDto>> Handle(GetEtlEltPipelinesAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = EtlEltPipelinesGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EtlEltPipelinesAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<EtlEltPipelinesAuditMetadataDto>.Fail("EtlEltPipelines readiness record was not found.", 404)
            : Response<EtlEltPipelinesAuditMetadataDto>.Success(EtlEltPipelinesMapper.ToAuditMetadata(entity));
    }
}
