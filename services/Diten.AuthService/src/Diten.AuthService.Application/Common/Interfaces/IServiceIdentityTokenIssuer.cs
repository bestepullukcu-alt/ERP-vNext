namespace Diten.AuthService.Application.Common.Interfaces;

public interface IServiceIdentityTokenIssuer
{
    ServiceIdentityTokenIssue Issue(Guid clientId, string serviceName, Guid tenantId, string audience);
}

public sealed record ServiceIdentityTokenIssue(string AccessToken, DateTimeOffset ExpiresAtUtc, int ExpiresInSeconds);
