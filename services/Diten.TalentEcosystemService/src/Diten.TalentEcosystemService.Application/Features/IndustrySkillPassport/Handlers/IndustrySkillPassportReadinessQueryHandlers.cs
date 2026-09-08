using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Handlers;

public sealed class GetIndustrySkillPassportReadinessListHandler
    : IRequestHandler<GetIndustrySkillPassportReadinessListQuery, Response<IReadOnlyList<IndustrySkillPassportReadinessListItemDto>>>
{
    private readonly IIndustrySkillPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustrySkillPassportReadinessListHandler(IIndustrySkillPassportReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<IndustrySkillPassportReadinessListItemDto>>> Handle(
        GetIndustrySkillPassportReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = IndustrySkillPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<IndustrySkillPassportReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<IndustrySkillPassportReadinessListItemDto>>.Success(rows.Select(IndustrySkillPassportMapper.ToListItem).ToList());
    }
}

public sealed class GetIndustrySkillPassportReadinessByIdHandler
    : IRequestHandler<GetIndustrySkillPassportReadinessByIdQuery, Response<IndustrySkillPassportReadinessDto>>
{
    private readonly IIndustrySkillPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustrySkillPassportReadinessByIdHandler(IIndustrySkillPassportReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustrySkillPassportReadinessDto>> Handle(GetIndustrySkillPassportReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = IndustrySkillPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySkillPassportReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<IndustrySkillPassportReadinessDto>.Fail("IndustrySkillPassport readiness record was not found.", 404)
            : Response<IndustrySkillPassportReadinessDto>.Success(IndustrySkillPassportMapper.ToDto(entity));
    }
}

public sealed class GetIndustrySkillPassportAuditMetadataHandler
    : IRequestHandler<GetIndustrySkillPassportAuditMetadataQuery, Response<IndustrySkillPassportAuditMetadataDto>>
{
    private readonly IIndustrySkillPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetIndustrySkillPassportAuditMetadataHandler(IIndustrySkillPassportReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustrySkillPassportAuditMetadataDto>> Handle(GetIndustrySkillPassportAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = IndustrySkillPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySkillPassportAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<IndustrySkillPassportAuditMetadataDto>.Fail("IndustrySkillPassport readiness record was not found.", 404)
            : Response<IndustrySkillPassportAuditMetadataDto>.Success(IndustrySkillPassportMapper.ToAuditMetadata(entity));
    }
}
