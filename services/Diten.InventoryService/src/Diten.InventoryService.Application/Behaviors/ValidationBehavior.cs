using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Diten.InventoryService.Application.Behaviors;

/// <summary>
/// Pipeline behavior 1/4 (pipeline-behaviors.md). Same shape as the ProcurementService template, plus the contract
/// error code: the FIRST failure's <see cref="ValidationFailure.ErrorCode"/> becomes <c>error.code</c> when it is a
/// declared inventory code, otherwise <see cref="InventoryErrorCodes.InvalidRequest"/>.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var code = InventoryErrorCodes.All.Contains(failures[0].ErrorCode)
            ? failures[0].ErrorCode
            : InventoryErrorCodes.InvalidRequest;
        var messages = failures.Select(f => f.ErrorMessage).ToList();

        var responseType = typeof(TResponse);
        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Response<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var failMethod = typeof(Response<>)
                .MakeGenericType(innerType)
                .GetMethod("Fail", [typeof(string), typeof(IReadOnlyList<string>), typeof(int)])!;
            return (TResponse)failMethod.Invoke(null, [code, messages.AsReadOnly(), 400])!;
        }

        throw new ValidationException(failures);
    }
}
