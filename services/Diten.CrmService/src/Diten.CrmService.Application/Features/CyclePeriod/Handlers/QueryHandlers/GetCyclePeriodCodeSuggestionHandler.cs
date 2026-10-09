using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>
/// WP-CAP-MODEL (K-2) — suggests <c>{PREFIX}-{YYYY}-{NN}</c> for a new period (<see cref="CyclePeriodCodeSuggestionRules"/>).
/// <para><b>Suggests, never decides.</b> It writes nothing and reserves nothing; two authors asking at once get the same
/// answer, and the create path's own uniqueness checks settle who keeps it.</para>
/// <para>The legal-entity / business-unit short code is a lookup, not a validation: an unreachable MDM or an unpublished
/// set degrades the prefix to <c>LE</c> / <c>BU</c> rather than failing the suggestion. Whether the scope may be
/// WRITTEN is still decided by the create path's fail-closed checks.</para>
/// </summary>
public sealed class GetCyclePeriodCodeSuggestionHandler
    : IRequestHandler<GetCyclePeriodCodeSuggestionQuery, Response<CyclePeriodCodeSuggestionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodRepository _periods;
    private readonly ICyclePeriodLegalEntityCatalog _legalEntities;
    private readonly IReferenceDataCatalogReader _references;

    public GetCyclePeriodCodeSuggestionHandler(
        ITenantContext tenant,
        ICyclePeriodRepository periods,
        ICyclePeriodLegalEntityCatalog legalEntities,
        IReferenceDataCatalogReader references)
    {
        _tenant = tenant;
        _periods = periods;
        _legalEntities = legalEntities;
        _references = references;
    }

    public async Task<Response<CyclePeriodCodeSuggestionDto>> Handle(
        GetCyclePeriodCodeSuggestionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CyclePeriodCodeSuggestionDto>.Fail("Tenant context is required.", 400);
        }

        if (CyclePeriodValidation.ValidateYear(request.Year) is { } yearFailure)
        {
            return Response<CyclePeriodCodeSuggestionDto>.Fail(
                CyclePeriodValidation.ToErrors(yearFailure), yearFailure.StatusCode);
        }

        var (scope, scopeFailure) = CyclePeriodScopeRules.Normalize(
            request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId);
        if (scopeFailure is not null || scope is null)
        {
            var failure = scopeFailure
                          ?? new CyclePeriodValidation.Failure("Scope is required.", CyclePeriodErrorCodes.ScopeTypeUnknown);
            return Response<CyclePeriodCodeSuggestionDto>.Fail(CyclePeriodValidation.ToErrors(failure), failure.StatusCode);
        }

        // Closed rows count: a sequence, like a code, is never recycled.
        var rowsOfYear = await _periods.ListByYearAsync(tenantId, request.Year, cancellationToken);
        if (CyclePeriodCodeSuggestionRules.NextFreeSequence(rowsOfYear, scope.ScopeType, scope.ScopeRef) is not { } sequence)
        {
            return Response<CyclePeriodCodeSuggestionDto>.Fail(
                new[]
                {
                    $"Every sequence of {request.Year} is already used at scope "
                    + $"{CyclePeriodScopeRules.Describe(scope.ScopeType, scope.ScopeRef)}.",
                    CyclePeriodErrorCodes.SequenceTaken
                },
                409);
        }

        var prefix = CyclePeriodCodeSuggestionRules.Prefix(scope, await CatalogCodeAsync(scope, cancellationToken));

        return Response<CyclePeriodCodeSuggestionDto>.Success(new CyclePeriodCodeSuggestionDto(
            CyclePeriodCodeSuggestionRules.Format(prefix, request.Year, sequence), sequence));
    }

    /// <summary>The legal entity's / business unit's short code from its catalog, or <c>null</c> (→ LE / BU).</summary>
    private async Task<string?> CatalogCodeAsync(
        CyclePeriodScopeRules.NormalizedScope scope, CancellationToken cancellationToken)
    {
        if (scope.IsLegalEntity)
        {
            var lookup = await _legalEntities.GetReferenceableAsync(cancellationToken);
            return lookup.Options.FirstOrDefault(o => o.LegalEntityId == scope.LegalEntityId)?.Code;
        }

        if (scope.IsBusinessUnit)
        {
            var set = await _references.GetPublishedValuesAsync(
                CyclePeriodReferenceSets.BusinessUnitSet, cancellationToken);
            return set.Values
                .FirstOrDefault(v => v.IsActive && !v.IsDeprecated
                                     && string.Equals(v.ValueCode?.Trim(), scope.BusinessUnitId, StringComparison.OrdinalIgnoreCase))
                ?.ValueCode;
        }

        return null;
    }
}
