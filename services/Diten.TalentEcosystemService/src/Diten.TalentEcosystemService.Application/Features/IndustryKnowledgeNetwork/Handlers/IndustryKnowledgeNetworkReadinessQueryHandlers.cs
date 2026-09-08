using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Handlers;

public sealed class GetIndustryKnowledgeNetworkReadinessListHandler
    : IRequestHandler<GetIndustryKnowledgeNetworkReadinessListQuery, Response<IReadOnlyList<IndustryKnowledgeNetworkReadinessListItemDto>>>
{
    private readonly IIndustryKnowledgeNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustryKnowledgeNetworkReadinessListHandler(IIndustryKnowledgeNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<IndustryKnowledgeNetworkReadinessListItemDto>>> Handle(
        GetIndustryKnowledgeNetworkReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = IndustryKnowledgeNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<IndustryKnowledgeNetworkReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<IndustryKnowledgeNetworkReadinessListItemDto>>.Success(rows.Select(IndustryKnowledgeNetworkMapper.ToListItem).ToList());
    }
}

public sealed class GetIndustryKnowledgeNetworkReadinessByIdHandler
    : IRequestHandler<GetIndustryKnowledgeNetworkReadinessByIdQuery, Response<IndustryKnowledgeNetworkReadinessDto>>
{
    private readonly IIndustryKnowledgeNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustryKnowledgeNetworkReadinessByIdHandler(IIndustryKnowledgeNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustryKnowledgeNetworkReadinessDto>> Handle(GetIndustryKnowledgeNetworkReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = IndustryKnowledgeNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustryKnowledgeNetworkReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<IndustryKnowledgeNetworkReadinessDto>.Fail("IndustryKnowledgeNetwork readiness record was not found.", 404)
            : Response<IndustryKnowledgeNetworkReadinessDto>.Success(IndustryKnowledgeNetworkMapper.ToDto(entity));
    }
}

public sealed class GetIndustryKnowledgeNetworkAuditMetadataHandler
    : IRequestHandler<GetIndustryKnowledgeNetworkAuditMetadataQuery, Response<IndustryKnowledgeNetworkAuditMetadataDto>>
{
    private readonly IIndustryKnowledgeNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustryKnowledgeNetworkAuditMetadataHandler(IIndustryKnowledgeNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustryKnowledgeNetworkAuditMetadataDto>> Handle(GetIndustryKnowledgeNetworkAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = IndustryKnowledgeNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustryKnowledgeNetworkAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<IndustryKnowledgeNetworkAuditMetadataDto>.Fail("IndustryKnowledgeNetwork readiness record was not found.", 404)
            : Response<IndustryKnowledgeNetworkAuditMetadataDto>.Success(IndustryKnowledgeNetworkMapper.ToAuditMetadata(entity));
    }
}
