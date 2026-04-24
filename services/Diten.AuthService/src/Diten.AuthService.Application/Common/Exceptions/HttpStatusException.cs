namespace Diten.AuthService.Application.Common.Exceptions;

public sealed class HttpStatusException : Exception
{
    public HttpStatusException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
