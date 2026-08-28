using FluentValidation;
using MediatR;

namespace Diten.Platform.Application.Contracts.Behaviors;

/// <summary>
/// Runs every registered validator for a request before the handler sees it, and refuses the request when any of
/// them fails.
///
/// <para><b>BL-040 — why there is no reflection here any more.</b> This class used to look
/// <c>Response&lt;T&gt;.Fail</c> up by name with a two-type signature so it could return a typed failure instead
/// of throwing. The real method takes four parameters, and optional parameters do not make <c>GetMethod</c>'s
/// type array match — so the lookup returned <c>null</c> on EVERY request, for four months, and the code fell
/// through to the throw below without anything anywhere saying so.</para>
///
/// <para><b>The defect was the reflection, not the type array.</b> Correcting the signature would have kept the
/// same failure mode alive: the next parameter added to <c>Fail</c> would break the match again, just as quietly.
/// It would ALSO have changed the response shape platform-wide, from <c>ValidationProblemDetails</c> to a
/// <c>Response&lt;T&gt;</c> envelope — and six client files read <c>problem.detail</c> off validation failures
/// today. So the lookup is gone entirely and this behaviour has exactly one outcome for a failed request: throw.
/// There is nothing left that can silently do the wrong thing.</para>
///
/// <para><b>Where the reason code comes from.</b> Nowhere here. <see cref="ValidationFailure"/> already carries
/// <c>ErrorCode</c>, so the failures thrown below reach
/// <c>GlobalExceptionHandler</c> with everything it needs to derive a stable code (see
/// <see cref="ValidationReasonCode"/>). Removing the construction from this class did not cost the pipeline any
/// information — it never had any that the exception does not.</para>
/// </summary>
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
                // The whole ValidationFailure travels, not just its message: PropertyName and ErrorCode are what
                // the reason code is derived from, and a list of strings would throw both away right here.
                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
