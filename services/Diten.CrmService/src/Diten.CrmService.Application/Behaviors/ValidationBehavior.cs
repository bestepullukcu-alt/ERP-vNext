using Diten.CrmService.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace Diten.CrmService.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(validator => validator.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            // Return a controlled 400 envelope (e.g. ValidFrom>ValidTo) instead of throwing → the API maps it to a
            // friendly 400 rather than a 500. Falls back to the exception only for non-Response<T> responses.
            var messages = failures.Select(f => f.ErrorMessage).Distinct().ToList();
            if (TryBuildFailResponse(messages, out var failResponse))
            {
                return failResponse;
            }

            throw new ValidationException(failures);
        }

        return await next();
    }

    private static bool TryBuildFailResponse(IReadOnlyList<string> messages, out TResponse response)
    {
        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Response<>))
        {
            var failMethod = responseType.GetMethod(
                nameof(Response<object>.Fail),
                new[] { typeof(IReadOnlyList<string>), typeof(int) });
            if (failMethod is not null)
            {
                response = (TResponse)failMethod.Invoke(null, new object[] { messages, 400 })!;
                return true;
            }
        }

        response = default!;
        return false;
    }
}
