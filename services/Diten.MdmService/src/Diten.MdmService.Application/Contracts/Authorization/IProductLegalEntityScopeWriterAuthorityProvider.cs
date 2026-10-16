using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Contracts.Authorization;

public interface IProductLegalEntityScopeWriterAuthorityProvider
{
    Task<ProductLegalEntityScopeVerifiedWriterAuthority?> ResolveForegroundReplaceAsync(
        Guid aggregateId,
        ProductLegalEntityScopeMutationIdentity mutation,
        CancellationToken cancellationToken = default);
}
