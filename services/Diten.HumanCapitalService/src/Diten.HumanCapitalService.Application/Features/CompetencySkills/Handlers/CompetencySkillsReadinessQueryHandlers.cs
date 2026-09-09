using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Handlers;

public sealed class GetCompetencySkillsReadinessListHandler
    : IRequestHandler<GetCompetencySkillsReadinessListQuery, Response<IReadOnlyList<CompetencySkillsReadinessListItemDto>>>
{
    private readonly ICompetencySkillsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCompetencySkillsReadinessListHandler(ICompetencySkillsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<CompetencySkillsReadinessListItemDto>>> Handle(
        GetCompetencySkillsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = CompetencySkillsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<CompetencySkillsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<CompetencySkillsReadinessListItemDto>>.Success(rows.Select(CompetencySkillsMapper.ToListItem).ToList());
    }
}

public sealed class GetCompetencySkillsReadinessByIdHandler
    : IRequestHandler<GetCompetencySkillsReadinessByIdQuery, Response<CompetencySkillsReadinessDto>>
{
    private readonly ICompetencySkillsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCompetencySkillsReadinessByIdHandler(ICompetencySkillsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CompetencySkillsReadinessDto>> Handle(GetCompetencySkillsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = CompetencySkillsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CompetencySkillsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CompetencySkillsReadinessDto>.Fail("CompetencySkills readiness record was not found.", 404)
            : Response<CompetencySkillsReadinessDto>.Success(CompetencySkillsMapper.ToDto(entity));
    }
}

public sealed class GetCompetencySkillsAuditMetadataHandler
    : IRequestHandler<GetCompetencySkillsAuditMetadataQuery, Response<CompetencySkillsAuditMetadataDto>>
{
    private readonly ICompetencySkillsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCompetencySkillsAuditMetadataHandler(ICompetencySkillsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CompetencySkillsAuditMetadataDto>> Handle(GetCompetencySkillsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CompetencySkillsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CompetencySkillsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CompetencySkillsAuditMetadataDto>.Fail("CompetencySkills readiness record was not found.", 404)
            : Response<CompetencySkillsAuditMetadataDto>.Success(CompetencySkillsMapper.ToAuditMetadata(entity));
    }
}
