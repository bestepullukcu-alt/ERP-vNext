namespace Diten.Platform.API.Models;

public sealed record Response<T>(bool Succeeded, string Message, T Data)
{
    public static Response<T> Ok(T data, string message = "OK") => new(true, message, data);
}
