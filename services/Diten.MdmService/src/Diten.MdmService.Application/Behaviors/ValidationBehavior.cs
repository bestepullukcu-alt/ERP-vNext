using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.MdmService.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();
        if (validators.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(validator => validator.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .Select(error => error.ErrorMessage)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Response<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var failMethod = typeof(Response<>)
                .MakeGenericType(innerType)
                .GetMethod(nameof(Response<NoContent>.Fail), [typeof(IReadOnlyList<string>), typeof(int)])!;

            return (TResponse)failMethod.Invoke(null, [failures.AsReadOnly(), 400])!;
        }

        throw new ValidationException(failures.Select(error => new FluentValidation.Results.ValidationFailure(string.Empty, error)));
    }
}
