using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Handlers;

public sealed class GetIndustryTalentPoolReadinessListHandler
    : IRequestHandler<GetIndustryTalentPoolReadinessListQuery, Response<IReadOnlyList<IndustryTalentPoolReadinessListItemDto>>>
{
    private readonly IIndustryTalentPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustryTalentPoolReadinessListHandler(IIndustryTalentPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<IndustryTalentPoolReadinessListItemDto>>> Handle(
        GetIndustryTalentPoolReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = IndustryTalentPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<IndustryTalentPoolReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<IndustryTalentPoolReadinessListItemDto>>.Success(rows.Select(IndustryTalentPoolMapper.ToListItem).ToList());
    }
}

public sealed class GetIndustryTalentPoolReadinessByIdHandler
    : IRequestHandler<GetIndustryTalentPoolReadinessByIdQuery, Response<IndustryTalentPoolReadinessDto>>
{
    private readonly IIndustryTalentPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustryTalentPoolReadinessByIdHandler(IIndustryTalentPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustryTalentPoolReadinessDto>> Handle(GetIndustryTalentPoolReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = IndustryTalentPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustryTalentPoolReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<IndustryTalentPoolReadinessDto>.Fail("IndustryTalentPool readiness record was not found.", 404)
            : Response<IndustryTalentPoolReadinessDto>.Success(IndustryTalentPoolMapper.ToDto(entity));
    }
}

public sealed class GetIndustryTalentPoolAuditMetadataHandler
    : IRequestHandler<GetIndustryTalentPoolAuditMetadataQuery, Response<IndustryTalentPoolAuditMetadataDto>>
{
    private readonly IIndustryTalentPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustryTalentPoolAuditMetadataHandler(IIndustryTalentPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustryTalentPoolAuditMetadataDto>> Handle(GetIndustryTalentPoolAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = IndustryTalentPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustryTalentPoolAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<IndustryTalentPoolAuditMetadataDto>.Fail("IndustryTalentPool readiness record was not found.", 404)
            : Response<IndustryTalentPoolAuditMetadataDto>.Success(IndustryTalentPoolMapper.ToAuditMetadata(entity));
    }
}
