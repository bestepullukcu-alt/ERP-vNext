using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class InternalEventAuthService : IInternalEventAuthService
{
    private readonly InternalEventAuthSettings _settings;

    public InternalEventAuthService(IOptions<InternalEventAuthSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool IsAuthorized(string? apiKeyHeaderValue)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(apiKeyHeaderValue))
        {
            return false;
        }

        return string.Equals(_settings.ApiKey, apiKeyHeaderValue, StringComparison.Ordinal);
    }
}
