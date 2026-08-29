using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Handlers.QueryHandlers;

public sealed class ResolveTrustedLegalEntityScopeHandler : IRequestHandler<ResolveTrustedLegalEntityScopeQuery, Response<TrustedLegalEntityScopeResolution>>
{
    private readonly IOrgDataScopeCandidateResolver _resolver;
    private readonly TimeProvider _timeProvider;

    public ResolveTrustedLegalEntityScopeHandler(
        IOrgDataScopeCandidateResolver resolver,
        TimeProvider timeProvider)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Response<TrustedLegalEntityScopeResolution>> Handle(
        ResolveTrustedLegalEntityScopeQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var set = await _resolver.ResolveAsync(request.TenantId, request.SubjectId, cancellationToken);
            var ids = set.LegalEntityIds;
            if (ids.Count > TrustedLegalEntityScopeResolutionLimits.MaxCandidates
                || ids.Any(id => id == Guid.Empty)
                || ids.Distinct().Count() != ids.Count
                || !ids.SequenceEqual(ids.OrderBy(id => id.ToString("D"), StringComparer.Ordinal)))
            {
                return Response<TrustedLegalEntityScopeResolution>.Fail(
                    "LEGAL_ENTITY_SCOPE_CONTRACT_INVALID",
                    409);
            }

            return Response<TrustedLegalEntityScopeResolution>.Success(
                new TrustedLegalEntityScopeResolution(
                    request.TenantId,
                    request.SubjectId,
                    request.ModuleCode,
                    request.PermissionKey,
                    _timeProvider.GetUtcNow(),
                    ids),
                200);
        }
        catch (OrgDataScopeCandidateContractException)
        {
            return Response<TrustedLegalEntityScopeResolution>.Fail("LEGAL_ENTITY_SCOPE_CONTRACT_INVALID", 409);
        }
        catch (OrgDataScopeCandidateUnavailableException)
        {
            return Response<TrustedLegalEntityScopeResolution>.Fail("LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE", 503);
        }
    }
}
