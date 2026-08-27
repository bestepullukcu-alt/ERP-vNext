namespace Diten.Platform.Common.Authorization.Fu16;

public sealed record Fu16AuthenticationFailure(int HttpStatus, string StableCode)
{
    public static Fu16AuthenticationFailure BadRequest(string code) => new(400, code);
    public static Fu16AuthenticationFailure Unauthorized(string code) => new(401, code);
    public static Fu16AuthenticationFailure Forbidden(string code) => new(403, code);
    public static Fu16AuthenticationFailure Conflict(string code) => new(409, code);
    public static Fu16AuthenticationFailure Unavailable(string code) => new(503, code);
}

public sealed class Fu16AuthenticationFailureException(Fu16AuthenticationFailure failure)
    : Exception(failure.StableCode)
{
    public Fu16AuthenticationFailure Failure { get; } = failure;
}
