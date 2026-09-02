using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Handlers;

public sealed class GetCandidateProfileListHandler
    : IRequestHandler<GetCandidateProfileListQuery, Response<IReadOnlyList<CandidateProfileListItemDto>>>
{
    private readonly ITepCandidateProfileMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCandidateProfileListHandler(ITepCandidateProfileMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<CandidateProfileListItemDto>>> Handle(GetCandidateProfileListQuery request, CancellationToken ct)
    {
        var tenant = CandidateProfileGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<CandidateProfileListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var items = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<CandidateProfileListItemDto>>.Success(items.Select(CandidateProfileMapper.ToListItemDto).ToList());
    }
}

public sealed class GetCandidateProfileByIdHandler
    : IRequestHandler<GetCandidateProfileByIdQuery, Response<CandidateProfileDto>>
{
    private readonly ITepCandidateProfileMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCandidateProfileByIdHandler(ITepCandidateProfileMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<CandidateProfileDto>> Handle(GetCandidateProfileByIdQuery request, CancellationToken ct)
    {
        var tenant = CandidateProfileGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateProfileDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<CandidateProfileDto>.Fail("Candidate profile record was not found.", 404)
            : Response<CandidateProfileDto>.Success(CandidateProfileMapper.ToDto(entity));
    }
}

public sealed class GetCandidateProfileAuditMetadataHandler
    : IRequestHandler<GetCandidateProfileAuditMetadataQuery, Response<CandidateProfileAuditMetadataDto>>
{
    private readonly ITepCandidateProfileMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCandidateProfileAuditMetadataHandler(ITepCandidateProfileMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<CandidateProfileAuditMetadataDto>> Handle(GetCandidateProfileAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CandidateProfileGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateProfileAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<CandidateProfileAuditMetadataDto>.Fail("Candidate profile record was not found.", 404)
            : Response<CandidateProfileAuditMetadataDto>.Success(new CandidateProfileAuditMetadataDto(
                entity.Id,
                entity.LocalAuditEvidenceRetentionState,
                entity.DependencyStates.Select(CandidateProfileMapper.ToDto).ToList(),
                entity.DeferredReason));
    }
}
