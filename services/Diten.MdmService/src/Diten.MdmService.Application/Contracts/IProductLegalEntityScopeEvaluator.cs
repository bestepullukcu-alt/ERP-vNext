using Diten.MdmService.Application.Features.ProductLegalEntityScopes;

namespace Diten.MdmService.Application.Contracts;

public interface IProductLegalEntityScopeEvaluator
{
    ProductLegalEntityScopeEvaluationResult Evaluate(ProductLegalEntityScopeEvaluationRequest request);
}
