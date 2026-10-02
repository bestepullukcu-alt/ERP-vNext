using FluentValidation.Results;

namespace Diten.AuthService.Application.Common;

/// <summary>
/// WP-USERS-ERROR-CODES-01 — the ONE list of validator error-code prefixes that travel on the wire as
/// <c>errorCodes</c>, and the one helper that turns FluentValidation failures into them. Two doors use it and behave
/// the same: <see cref="Behaviors.ExceptionHandlingBehavior{TRequest,TResponse}"/> (a failure thrown INSIDE a handler,
/// e.g. the tenant password policy → the Response envelope) and the Api's <c>GlobalExceptionHandler</c> (a pipeline
/// validator's failure → the "Validation failed" body). A code travels only when it starts with one of these;
/// everything else (FluentValidation's own defaults such as "NotEmptyValidator", or a feature that has not joined
/// yet) stays text-only, exactly as before.
/// </summary>
public static class EnvelopeErrorCodePrefixes
{
    public const string Password = PasswordErrorCodes.Prefix;
    public const string User = "USER_";
    public const string Role = "ROLE_";

    public static readonly IReadOnlyList<string> All = [Password, User, Role];

    public static bool Allows(string? code)
        => !string.IsNullOrEmpty(code) && All.Any(prefix => code.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// The allowed codes of <paramref name="failures"/>, each once, in order, with the params the rule attached
    /// (<c>CustomState</c> as a string dictionary, e.g. <c>{ "maxLength": "128" }</c>). Empty when none is allowed.
    /// </summary>
    public static IReadOnlyList<ResponseError> Extract(IEnumerable<ValidationFailure> failures)
    {
        var codes = new List<ResponseError>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var failure in failures)
        {
            var code = failure.ErrorCode;
            if (!Allows(code) || !seen.Add(code))
            {
                continue;
            }

            codes.Add(new ResponseError(code, failure.CustomState as IReadOnlyDictionary<string, string>));
        }

        return codes;
    }
}
