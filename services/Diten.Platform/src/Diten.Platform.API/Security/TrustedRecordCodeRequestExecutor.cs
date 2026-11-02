using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0040-FU02 §8.2 / §8.5 — the gate of <c>POST /api/internal/record-codes/allocate</c>. The controller is anonymous to
/// the framework; every refusal is here, in the pack's order, and the handler is reached only past all of them.
///
/// <para><b>The request's user is the forwarded token.</b> The token is validated explicitly under the JWT bearer scheme and
/// <see cref="HttpContext.User"/> is set to that principal BEFORE anything downstream is resolved: the in-transaction audit
/// door names its actor from <c>ITenantAuthorizationContext</c>, which reads the principal when it is constructed
/// (<c>JwtTenantAuthorizationContext</c>). The tenant is the token's, entered with <see cref="TenantScope.Begin"/>.</para>
/// </summary>
public sealed class TrustedRecordCodeRequestExecutor : ITrustedRecordCodeRequestExecutor
{
    public const string CredentialIdHeader = "X-Record-Code-Credential-Id";
    public const string CredentialSecretHeader = "X-Record-Code-Credential";
    public const string AudienceHeader = "X-Record-Code-Audience";

    /// <summary>RECORD-CODE-ALLOCATE 1.0.0 §2: a body above 4 KB is 413, before anything else is read.</summary>
    public const int MaxBodyBytes = 4 * 1024;

    private readonly ITrustedRecordCodeCredentialAuthenticator _credentialAuthenticator;
    private readonly ITrustedRecordCodeJwtContext _jwtContext;
    private readonly ITenantContext _tenantContext;

    public TrustedRecordCodeRequestExecutor(
        ITrustedRecordCodeCredentialAuthenticator credentialAuthenticator,
        ITrustedRecordCodeJwtContext jwtContext,
        ITenantContext tenantContext)
    {
        _credentialAuthenticator = credentialAuthenticator ?? throw new ArgumentNullException(nameof(credentialAuthenticator));
        _jwtContext = jwtContext ?? throw new ArgumentNullException(nameof(jwtContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext context,
        CancellationToken cancellationToken,
        Func<TrustedRecordCodeCaller, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure)
    {
        // Step 1 — size.
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            return failure(413, RecordCodeReasonCodes.RequestTooLarge);
        }

        // Step 2 — the credential: three headers, each exactly once.
        if (context.Request.Headers[CredentialIdHeader].Count != 1
            || context.Request.Headers[CredentialSecretHeader].Count != 1
            || context.Request.Headers[AudienceHeader].Count != 1)
        {
            return failure(401, RecordCodeReasonCodes.Unauthenticated);
        }

        var credential = _credentialAuthenticator.Authenticate(
            context.Request.Headers[CredentialIdHeader][0],
            context.Request.Headers[CredentialSecretHeader][0],
            context.Request.Headers[AudienceHeader][0]);
        if (!credential.Authenticated)
        {
            return failure(
                credential.Forbidden ? 403 : 401,
                credential.Forbidden ? RecordCodeReasonCodes.Forbidden : RecordCodeReasonCodes.Unauthenticated);
        }

        // Step 2 — the forwarded user token; it becomes the request's user before anything else is resolved.
        var jwt = await _jwtContext.ResolveAsync(context);
        if (jwt.Principal is not null)
        {
            context.User = jwt.Principal;
        }

        if (!jwt.Authenticated)
        {
            return failure(401, RecordCodeReasonCodes.Unauthenticated);
        }

        if (!jwt.Authorized || !jwt.TenantId.HasValue || !jwt.SubjectId.HasValue)
        {
            return failure(403, RecordCodeReasonCodes.Forbidden);
        }

        using (TenantScope.Begin(_tenantContext, jwt.TenantId.Value))
        {
            return await action(
                new TrustedRecordCodeCaller(
                    jwt.TenantId.Value,
                    jwt.SubjectId.Value,
                    credential.CallerService!,
                    credential.AllowedRecordTypes),
                cancellationToken);
        }
    }
}
