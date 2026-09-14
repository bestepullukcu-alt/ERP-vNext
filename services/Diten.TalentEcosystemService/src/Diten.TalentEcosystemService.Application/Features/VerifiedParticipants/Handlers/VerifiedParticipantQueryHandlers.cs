using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;

public sealed class GetVerifiedParticipantAccessListHandler
    : IRequestHandler<GetVerifiedParticipantAccessListQuery, Response<IReadOnlyList<VerifiedParticipantAccessListItemDto>>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetVerifiedParticipantAccessListHandler(ITepVerifiedParticipantAccessRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<VerifiedParticipantAccessListItemDto>>> Handle(
        GetVerifiedParticipantAccessListQuery request,
        CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<VerifiedParticipantAccessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<VerifiedParticipantAccessListItemDto>>.Success(items.Select(VerifiedParticipantMapper.ToListItemDto).ToList());
    }
}

public sealed class GetVerifiedParticipantAccessByIdHandler
    : IRequestHandler<GetVerifiedParticipantAccessByIdQuery, Response<VerifiedParticipantAccessDto>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetVerifiedParticipantAccessByIdHandler(ITepVerifiedParticipantAccessRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<VerifiedParticipantAccessDto>> Handle(GetVerifiedParticipantAccessByIdQuery request, CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedParticipantAccessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<VerifiedParticipantAccessDto>.Fail("Verified participant access record was not found.", 404)
            : Response<VerifiedParticipantAccessDto>.Success(VerifiedParticipantMapper.ToDto(entity));
    }
}

public sealed class GetVerifiedParticipantAccessAuditMetadataHandler
    : IRequestHandler<GetVerifiedParticipantAccessAuditMetadataQuery, Response<VerifiedParticipantAuditMetadataDto>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetVerifiedParticipantAccessAuditMetadataHandler(ITepVerifiedParticipantAccessRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<VerifiedParticipantAuditMetadataDto>> Handle(GetVerifiedParticipantAccessAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedParticipantAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<VerifiedParticipantAuditMetadataDto>.Fail("Verified participant access record was not found.", 404)
            : Response<VerifiedParticipantAuditMetadataDto>.Success(new VerifiedParticipantAuditMetadataDto(
                entity.Id,
                entity.LocalAuditEvidenceRetentionState,
                entity.DependencyStates.Select(VerifiedParticipantMapper.ToDto).ToList(),
                entity.DeferredReason));
    }
}
