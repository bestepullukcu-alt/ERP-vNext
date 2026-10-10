using Diten.SupplyChainService.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// R-2 (PR #134). The integration factories boot the real host, and production registers
/// <c>MdmLegalEntityScopeValidator</c> — an HttpClient aimed at MDM. A test host has no MDM and no
/// <c>LegalEntity:ReferenceBaseUrl</c>, so the validator fail-closes to Unavailable and every write answered
/// 503 DEPENDENCY_UNAVAILABLE: 76 of the suite's host tests went red the first time R-8 measured them. That is
/// the validator behaving correctly, not a defect — the test host simply has nothing to ask.
///
/// So the factories replace it. Deliberately with the always-Valid stub rather than a known-id set: the
/// foreign-legal-entity rejections in these suites are REPOSITORY facts, not validator facts — a request with an
/// unknown legal entity is scoped to it and finds nothing, which is the 404 they assert. Loads' isolation test
/// goes further and creates under freshly minted legal entities on purpose, so a known-id stub would break the
/// very isolation it proves.
///
/// What this does NOT prove is that production wires the real validator. That is a separate fact, asserted in
/// CompositionRootGuardTests — without it this replacement would be K23's shape: a dependency that only ever
/// runs as a stub.
/// </summary>
public static class TestHostLegalEntities
{
    public static IServiceCollection StubLegalEntityValidation(this IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Singleton<ILegalEntityScopeValidator>(StubLegalEntityScopeValidator.Valid));
}
