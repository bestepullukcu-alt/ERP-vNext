using FluentValidation;
using MediatR;
using System.Reflection;

namespace Diten.Platform.Application.Contracts.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(error => error != null)
                .ToList();

            if (failures.Count != 0)
            {
                var response = TryCreateFailureResponse(failures.Select(x => x.ErrorMessage).ToList(), 400);
                if (response is not null)
                {
                    return response;
                }

                throw new ValidationException(failures);
            }
        }

        return await next();
    }

    private static TResponse? TryCreateFailureResponse(IReadOnlyList<string> errors, int statusCode)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition().FullName != "Diten.Platform.Application.Common.Response`1")
        {
            return default;
        }

        var failMethod = responseType.GetMethod(
            "Fail",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(IReadOnlyList<string>), typeof(int)]);
        return failMethod is null ? default : (TResponse?)failMethod.Invoke(null, [errors, statusCode]);
    }
}
