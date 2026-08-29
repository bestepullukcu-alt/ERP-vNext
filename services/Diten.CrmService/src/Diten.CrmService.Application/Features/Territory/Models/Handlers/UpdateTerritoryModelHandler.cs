using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Models.Handlers;

public sealed class UpdateTerritoryModelHandler : IRequestHandler<UpdateTerritoryModelCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryReferenceValidator _references;

    public UpdateTerritoryModelHandler(ITenantContext tenant, ITerritoryModelRepository models, ITerritoryReferenceValidator references)
    {
        _tenant = tenant;
        _models = models;
        _references = references;
    }

    public async Task<Response<bool>> Handle(UpdateTerritoryModelCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (model is null)
        {
            return Response<bool>.Fail("Territory model not found.", 404);
        }

        // Active/review/approved/superseded/archived models are immutable in FU01 (activation is a later FU).
        if (!string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Only a draft territory model can be updated.", 409);
        }

        // FU02A: business-unit scopes are fail-closed validated against MOD-0048 published values (no fallback).
        var (businessScopes, scopeError) = await TerritoryBusinessScopeResolver.ResolveAsync(
            request.BusinessScopes, _references, cancellationToken);
        if (scopeError is not null)
        {
            return Response<bool>.Fail(scopeError, 400);
        }

        model.Name = request.Name.Trim();
        model.CountryScope = request.CountryScope?.Trim();
        model.DivisionScope = request.DivisionScope?.Trim();
        model.BusinessScopes = businessScopes;
        model.EffectiveFrom = request.EffectiveFrom;
        model.EffectiveTo = request.EffectiveTo;
        model.ChangeReason = request.ChangeReason?.Trim();
        model.CorrelationId = request.CorrelationId?.Trim();
        model.UpdatedAt = DateTimeOffset.UtcNow;

        await _models.UpdateAsync(model, cancellationToken);
        return Response<bool>.Success(true);
    }
}
