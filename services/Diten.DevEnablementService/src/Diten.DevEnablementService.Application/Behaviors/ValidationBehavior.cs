using Diten.Shared.Core;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Diten.DevEnablementService.Application.Behaviors;

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
            .Select(f => f.ErrorMessage)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var responseType = typeof(TResponse);
        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Response<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var failMethod = typeof(Response<>)
                .MakeGenericType(innerType)
                .GetMethod("Fail", [typeof(IReadOnlyList<string>), typeof(int)])!;
            return (TResponse)failMethod.Invoke(null, [failures.AsReadOnly(), 400])!;
        }

        throw new ValidationException(failures.Select(f => new ValidationFailure("", f)));
    }
}
