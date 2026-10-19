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
    private readonly IServiceClientIdentityRepository _identities;
    private readonly IServiceClientTenantGrantRepository _grants;
    private readonly IServiceClientCredentialVerifier _credentials;
    private readonly IServiceIdentityTokenIssuer _issuer;
    private readonly IAuthAuditService _audit;
    private readonly TimeProvider _timeProvider;

    /// <summary>The issued token is not handed out unless its security event is recorded (fail closed, K2).</summary>
    public const string AuditUnavailableCode = "SERVICE_TOKEN_AUDIT_UNAVAILABLE";

    public IssueServiceIdentityTokenHandler(
        IServiceClientIdentityRepository identities,
        IServiceClientTenantGrantRepository grants,
        IServiceClientCredentialVerifier credentials,
        IServiceIdentityTokenIssuer issuer,
        IAuthAuditService audit,
        TimeProvider timeProvider)
    {
        _identities = identities;
        _grants = grants;
        _credentials = credentials;
        _issuer = issuer;
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
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
                await DeniedAsync(request, now, ServiceIdentityTokenAudit.Results.UnknownClient, budget.Token);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client authentication failed.", 401);
            }

            var stateCoherent = _credentials.IsStateCoherent(identity);
            var credentialValid = _credentials.Verify(identity, request.ClientSecret, now);
            if (identity.IsRevoked || !credentialValid)
            {
                await DeniedAsync(request, now, identity.IsRevoked
                    ? ServiceIdentityTokenAudit.Results.ClientRevoked
                    : ServiceIdentityTokenAudit.Results.InvalidCredential, budget.Token);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client authentication failed.", 401);
            }

            if (!stateCoherent)
            {
                await DeniedAsync(request, now, ServiceIdentityTokenAudit.Results.CredentialStateInconsistent, budget.Token);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client credential state is inconsistent.", 409);
            }

            if (!ServiceIdentityTokenAudiencePolicy.TryResolveIdentityAudience(identity.AllowedAudience, out var identityAudience))
            {
                await DeniedAsync(request, now, ServiceIdentityTokenAudit.Results.PurposeInconsistent, budget.Token);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client purpose is inconsistent.", 409);
            }

            if (!ServiceIdentityTokenAudiencePolicy.IsAllowedPair(identity.ServiceName, request.Audience)
                || !string.Equals(identityAudience, request.Audience, StringComparison.Ordinal))
            {
                await DeniedAsync(request, now, ServiceIdentityTokenAudit.Results.AudienceNotPermitted, budget.Token);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client is not permitted for the requested audience.", 403);
            }

            var granted = await _grants.HasEnabledGrantAsync(request.TenantId, identity.Id, request.Audience, budget.Token);
            if (!granted)
            {
                await DeniedAsync(request, now, ServiceIdentityTokenAudit.Results.TenantGrantMissing, budget.Token);
                return Response<ServiceIdentityTokenResponse>.Fail("Service client is not granted for the requested tenant and audience.", 403);
            }

            var token = _issuer.Issue(identity.Id, identity.ServiceName, request.TenantId, request.Audience);

            // Recorded BEFORE the token leaves: a token whose issue is not in the log is never handed out.
            if (!await TryRecordIssuedAsync(request, now, budget.Token))
            {
                return Response<ServiceIdentityTokenResponse>.Fail(
                    "Service token audit is unavailable.", [new ResponseError(AuditUnavailableCode)], 503);
            }

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

    private async Task<bool> TryRecordIssuedAsync(IssueServiceIdentityTokenCommand request, DateTimeOffset now, CancellationToken ct)
    {
        var day = ServiceIdentityTokenAudit.Day(now);
        try
        {
            // Create-if-absent: "already recorded today" is as good as written — the event is in the log.
            await _audit.WriteOnceAsync(
                ServiceIdentityTokenAudit.IssuedAuditId(request.ClientCode, request.TenantId, request.Audience, day),
                ServiceIdentityTokenAudit.IssuedEvent,
                null,
                request.TenantId,
                ServiceIdentityTokenAudit.Metadata(request.ClientCode, request.Audience, day, null),
                ct);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// A refusal is recorded too, but its outcome does not depend on the record: the caller is refused either way,
    /// with the same answer it gets today, so a log outage changes nothing a client can observe.
    /// </summary>
    private async Task DeniedAsync(IssueServiceIdentityTokenCommand request, DateTimeOffset now, string result, CancellationToken ct)
    {
        var day = ServiceIdentityTokenAudit.Day(now);
        // A registered client's code is a fact worth keeping; an unknown one is whatever the caller made up.
        var clientCode = result == ServiceIdentityTokenAudit.Results.UnknownClient
            ? ServiceIdentityTokenAudit.UnknownClientCode
            : request.ClientCode;
        try
        {
            await _audit.WriteOnceAsync(
                ServiceIdentityTokenAudit.DeniedAuditId(clientCode, request.TenantId, request.Audience, day, result),
                ServiceIdentityTokenAudit.DeniedEvent,
                null,
                request.TenantId,
                ServiceIdentityTokenAudit.Metadata(clientCode, request.Audience, day, result),
                ct);
        }
        catch (Exception)
        {
            // Refused regardless — a slow or failed log write (the budget running out included) must not turn a
            // 401/403/409 into a 504 or a 503; see the summary. A caller who cancelled is stopped by the controller.
        }
    }

}
