using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Handlers;

public sealed class GetProfessionalReputationLedgerReadinessListHandler
    : IRequestHandler<GetProfessionalReputationLedgerReadinessListQuery, Response<IReadOnlyList<ProfessionalReputationLedgerReadinessListItemDto>>>
{
    private readonly IProfessionalReputationLedgerReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetProfessionalReputationLedgerReadinessListHandler(IProfessionalReputationLedgerReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<ProfessionalReputationLedgerReadinessListItemDto>>> Handle(
        GetProfessionalReputationLedgerReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = ProfessionalReputationLedgerGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ProfessionalReputationLedgerReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<ProfessionalReputationLedgerReadinessListItemDto>>.Success(rows.Select(ProfessionalReputationLedgerMapper.ToListItem).ToList());
    }
}

public sealed class GetProfessionalReputationLedgerReadinessByIdHandler
    : IRequestHandler<GetProfessionalReputationLedgerReadinessByIdQuery, Response<ProfessionalReputationLedgerReadinessDto>>
{
    private readonly IProfessionalReputationLedgerReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetProfessionalReputationLedgerReadinessByIdHandler(IProfessionalReputationLedgerReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ProfessionalReputationLedgerReadinessDto>> Handle(GetProfessionalReputationLedgerReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = ProfessionalReputationLedgerGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ProfessionalReputationLedgerReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<ProfessionalReputationLedgerReadinessDto>.Fail("ProfessionalReputationLedger readiness record was not found.", 404)
            : Response<ProfessionalReputationLedgerReadinessDto>.Success(ProfessionalReputationLedgerMapper.ToDto(entity));
    }
}

public sealed class GetProfessionalReputationLedgerAuditMetadataHandler
    : IRequestHandler<GetProfessionalReputationLedgerAuditMetadataQuery, Response<ProfessionalReputationLedgerAuditMetadataDto>>
{
    private readonly IProfessionalReputationLedgerReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetProfessionalReputationLedgerAuditMetadataHandler(IProfessionalReputationLedgerReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ProfessionalReputationLedgerAuditMetadataDto>> Handle(GetProfessionalReputationLedgerAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ProfessionalReputationLedgerGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ProfessionalReputationLedgerAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<ProfessionalReputationLedgerAuditMetadataDto>.Fail("ProfessionalReputationLedger readiness record was not found.", 404)
            : Response<ProfessionalReputationLedgerAuditMetadataDto>.Success(ProfessionalReputationLedgerMapper.ToAuditMetadata(entity));
    }
}
