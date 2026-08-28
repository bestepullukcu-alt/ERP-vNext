using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Handlers.CommandHandlers;

public sealed class IssueServiceIdentityTokenHandler : IRequestHandler<IssueServiceIdentityTokenCommand, Response<ServiceIdentityTokenResponse>>
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private const string RequiredServiceName = "Diten.MDM";
    private const string RequiredAudience = "TRUSTED_AUDIT_SOURCE_INGEST";
    private readonly IServiceClientIdentityRepository _identities;
    private readonly IServiceClientTenantGrantRepository _grants;
    private readonly IServiceClientCredentialVerifier _credentials;
    private readonly IServiceIdentityTokenIssuer _issuer;
    private readonly TimeProvider _timeProvider;

    public IssueServiceIdentityTokenHandler(
        IServiceClientIdentityRepository identities,
        IServiceClientTenantGrantRepository grants,
        IServiceClientCredentialVerifier credentials,
        IServiceIdentityTokenIssuer issuer,
        TimeProvider timeProvider)
    {
        _identities = identities;
        _grants = grants;
        _credentials = credentials;
        _issuer = issuer;
        _timeProvider = timeProvider;
    }

    public async Task<Response<ServiceIdentityTokenResponse>> Handle(IssueServiceIdentityTokenCommand request, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(Budget);

        try
        {
            var identity = await _identities.GetByClientCodeAsync(request.ClientCode, budget.Token);
            var now = _timeProvider.GetUtcNow();
            if (identity is null)
            {
                _credentials.PerformUnknownClientWork(request.ClientSecret);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client authentication failed.", 401);
            }

            var stateCoherent = _credentials.IsStateCoherent(identity);
            var credentialValid = _credentials.Verify(identity, request.ClientSecret, now);
            if (identity.IsRevoked || !credentialValid)
                return Response<ServiceIdentityTokenResponse>.Fail("Service client authentication failed.", 401);

            if (!stateCoherent)
                return Response<ServiceIdentityTokenResponse>.Fail("Service client credential state is inconsistent.", 409);

            if (!string.Equals(identity.ServiceName, RequiredServiceName, StringComparison.Ordinal)
                || !string.Equals(request.Audience, RequiredAudience, StringComparison.Ordinal))
                return Response<ServiceIdentityTokenResponse>.Fail("Service client is not permitted for the requested audience.", 403);

            var granted = await _grants.HasEnabledGrantAsync(request.TenantId, identity.Id, request.Audience, budget.Token);
            if (!granted)
                return Response<ServiceIdentityTokenResponse>.Fail("Service client is not granted for the requested tenant and audience.", 403);

            var token = _issuer.Issue(identity.Id, identity.ServiceName, request.TenantId, request.Audience);
            return Response<ServiceIdentityTokenResponse>.Success(new ServiceIdentityTokenResponse(
                token.AccessToken, "Bearer", token.ExpiresInSeconds, token.ExpiresAtUtc));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Response<ServiceIdentityTokenResponse>.Fail("Service token issuance timed out.", 504);
        }
        catch (ServiceIdentityPersistenceUnavailableException)
        {
            return Response<ServiceIdentityTokenResponse>.Fail("Service identity persistence is unavailable.", 503);
        }
        catch (InvalidOperationException)
        {
            return Response<ServiceIdentityTokenResponse>.Fail("Service token issuer is unavailable.", 503);
        }
    }

}
