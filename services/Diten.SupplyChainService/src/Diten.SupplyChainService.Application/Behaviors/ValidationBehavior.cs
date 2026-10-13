using MediatR;
using FluentValidation;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Behaviors;
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
 where TRequest : notnull where TResponse : IResponse<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(request, ct)));
        return results.Any(r => !r.IsValid) ? TResponse.Fail("INVALID_REQUEST", 400) : await next();
    }
}
