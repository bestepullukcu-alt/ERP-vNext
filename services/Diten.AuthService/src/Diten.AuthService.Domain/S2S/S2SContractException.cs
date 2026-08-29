namespace Diten.AuthService.Domain.S2S;

public sealed class S2SContractException : ArgumentException
{
    public S2SContractException(string message, string? parameterName = null)
        : base(message, parameterName)
    {
    }
}
