namespace Diten.AuthService.Application.Common.Interfaces;

public interface IInternalEventAuthService
{
    bool IsAuthorized(string? apiKeyHeaderValue);
}
