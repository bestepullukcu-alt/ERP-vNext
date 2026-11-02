using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0040-FU02 §8.2 / §8.5 — the gate of <c>POST /api/internal/record-codes/allocate</c>. The controller is anonymous to
/// the framework; steps 1–3 are refused here, step 5 is parsed and step 8 evaluated here (the handler refuses and records
/// them), and the handler is reached only past the gate.
///
/// <para><b>The request's user is the forwarded token.</b> The token is validated explicitly under the JWT bearer scheme and
/// <see cref="HttpContext.User"/> is set to that principal BEFORE anything downstream is resolved: the in-transaction audit
/// door names its actor from <c>ITenantAuthorizationContext</c>, which reads the principal when it is constructed
/// (<c>JwtTenantAuthorizationContext</c>). The tenant is the token's, entered with <see cref="TenantScope.Begin"/>.</para>
///
/// <para><b>Platform actors.</b> A valid <c>platform_admin</c> / <c>partner_admin</c> token is refused 403 like any
/// non-tenant user, but — being a person who can be named — its refusal is recorded <c>Denied</c> under the platform audit
/// tenant (K-13), after asking the rate limiter in its own <c>platform-actor:</c> partition: past the limit nothing is
/// written (warning log + counter), the answer stays 403.</para>
/// </summary>
public sealed class TrustedRecordCodeRequestExecutor : ITrustedRecordCodeRequestExecutor
{
    public const string CredentialIdHeader = "X-Record-Code-Credential-Id";
    public const string CredentialSecretHeader = "X-Record-Code-Credential";
    public const string AudienceHeader = "X-Record-Code-Audience";

    /// <summary>RECORD-CODE-ALLOCATE 1.0.0 §2: a body above 4 KB is 413, before anything else is read.</summary>
    public const int MaxBodyBytes = 4 * 1024;

    /// <summary>§8.1 — the whole request; past it, 503 (the MDM read has its own 3 s inside).</summary>
    public static readonly TimeSpan RequestBudget = TimeSpan.FromSeconds(5);

    private static readonly HashSet<string> PlatformActorTypes = new(StringComparer.Ordinal) { "platform_admin", "partner_admin" };

    private readonly ITrustedRecordCodeCredentialAuthenticator _credentialAuthenticator;
    private readonly ITrustedRecordCodeJwtContext _jwtContext;
    private readonly ITenantContext _tenantContext;
    private readonly RecordCodeAllocateRateLimiter _rateLimiter;
    private readonly ILogger<TrustedRecordCodeRequestExecutor> _logger;

    public TrustedRecordCodeRequestExecutor(
        ITrustedRecordCodeCredentialAuthenticator credentialAuthenticator,
        ITrustedRecordCodeJwtContext jwtContext,
        ITenantContext tenantContext,
        RecordCodeAllocateRateLimiter rateLimiter,
        ILogger<TrustedRecordCodeRequestExecutor> logger)
    {
        _credentialAuthenticator = credentialAuthenticator ?? throw new ArgumentNullException(nameof(credentialAuthenticator));
        _jwtContext = jwtContext ?? throw new ArgumentNullException(nameof(jwtContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext context,
        CancellationToken cancellationToken,
        Func<TrustedRecordCodeCaller, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure)
    {
        // Step 1 — size: a declared or an actual body above 4 KB, before anything else.
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            return Refused(failure, 413, RecordCodeReasonCodes.RequestTooLarge, null, null);
        }

        var body = await ReadBoundedBodyAsync(context.Request, cancellationToken);
        if (body is null)
        {
            return Refused(failure, 413, RecordCodeReasonCodes.RequestTooLarge, null, null);
        }

        // Step 2 — the credential: three headers, each exactly once.
        if (context.Request.Headers[CredentialIdHeader].Count != 1
            || context.Request.Headers[CredentialSecretHeader].Count != 1
            || context.Request.Headers[AudienceHeader].Count != 1)
        {
            return Refused(failure, 401, RecordCodeReasonCodes.Unauthenticated, null, null);
        }

        var credential = _credentialAuthenticator.Authenticate(
            context.Request.Headers[CredentialIdHeader][0],
            context.Request.Headers[CredentialSecretHeader][0],
            context.Request.Headers[AudienceHeader][0]);
        if (!credential.Authenticated)
        {
            return credential.Forbidden
                ? Refused(failure, 403, RecordCodeReasonCodes.Forbidden, null, null)
                : Refused(failure, 401, RecordCodeReasonCodes.Unauthenticated, null, null);
        }

        // Step 2 — the forwarded user token; it becomes the request's user before anything else is resolved.
        var jwt = await _jwtContext.ResolveAsync(context);
        if (jwt.Principal is not null)
        {
            context.User = jwt.Principal;
        }

        if (!jwt.Authenticated)
        {
            return Refused(failure, 401, RecordCodeReasonCodes.Unauthenticated, credential.CallerService, null);
        }

        if (!jwt.Authorized || !jwt.TenantId.HasValue || !jwt.SubjectId.HasValue)
        {
            if (jwt.Principal is not null && jwt.ActorType is { } actorType && PlatformActorTypes.Contains(actorType))
            {
                await RecordPlatformActorRefusalAsync(context, credential, jwt.TenantClaim, cancellationToken);
            }

            return Refused(failure, 403, RecordCodeReasonCodes.Forbidden, credential.CallerService, null);
        }

        var tenantId = jwt.TenantId.Value;

        // Step 3 — the rate limit, per (consumer service, token tenant). Not audited: the named departure (K-10 / K-13).
        if (!_rateLimiter.TryAcquire(RecordCodeAllocateRateLimiter.TenantPartition(credential.ConsumerService!, tenantId)))
        {
            RecordCodeAllocateRateLimiter.RateLimitedTotal
                .WithLabels(credential.ConsumerService!, tenantId.ToString("D"), RecordCodeAllocateRateLimiter.RateLimitedReason)
                .Inc();
            return Refused(failure, 429, RecordCodeReasonCodes.RateLimited, credential.CallerService, tenantId);
        }

        // Step 5 is parsed here and step 8 evaluated here; the handler refuses (and records) both, after step 4.
        var parsed = TrustedRecordCodeAllocateRequestParser.Parse(context.Request.ContentType, body);
        var hasRequiredPermission = parsed.Request is { } request
            && RecordCodeTypeCatalog.TryGet(request.RecordType, out var definition)
            && definition.RequiredPermission is { } permission
            && jwt.HasExactPermission(permission);

        using (TenantScope.Begin(_tenantContext, tenantId))
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(RequestBudget);
            try
            {
                return await action(
                    new TrustedRecordCodeCaller(
                        tenantId,
                        jwt.SubjectId.Value,
                        credential.CallerService!,
                        credential.AllowedRecordTypes,
                        parsed.Request,
                        parsed.Errors,
                        hasRequiredPermission,
                        context.TraceIdentifier),
                    budget.Token);
            }
            catch (OperationCanceledException) when (budget.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return Refused(failure, 503, RecordCodeReasonCodes.Unavailable, credential.CallerService, tenantId);
            }
        }
    }

    /// <summary>
    /// K-13 — the platform-actor refusal, written only when the limiter's own platform-actor partition allows it. The
    /// writer is resolved from the request scope NOW, after <see cref="HttpContext.User"/> is set, so the door names the
    /// administrator. Best effort: a record that cannot be written is logged and the answer stays 403.
    /// </summary>
    private async Task RecordPlatformActorRefusalAsync(
        HttpContext context,
        TrustedRecordCodeCredentialResult credential,
        string? tenantClaim,
        CancellationToken cancellationToken)
    {
        if (!_rateLimiter.TryAcquire(RecordCodeAllocateRateLimiter.PlatformActorPartition(credential.ConsumerService!, tenantClaim)))
        {
            var label = RecordCodeAllocateRateLimiter.PlatformActorTenantLabel(tenantClaim);
            _logger.LogWarning(
                "Record-code platform-actor refusal over the rate limit, not recorded; source service {SourceService}, tenant {Tenant}, correlation {CorrelationId}.",
                credential.CallerService, label, context.TraceIdentifier);
            RecordCodeAllocateRateLimiter.RateLimitedTotal
                .WithLabels(credential.ConsumerService!, label, RecordCodeAllocateRateLimiter.PlatformActorRefusalReason)
                .Inc();
            return;
        }

        try
        {
            var written = await RecordCodeAuditIntent.EnqueueRefusalAsync(
                context.RequestServices.GetRequiredService<ITransactionalAuditRefusalWriter>(),
                context.RequestServices.GetRequiredService<IPlatformTransactionExecutor>(),
                new RecordCodeAllocationRefusal(
                    AuditTenantIds.PlatformSystemTenantId,
                    RecordCodeAllocateRateLimiter.PlatformActorTargetTenant(tenantClaim),
                    credential.CallerService,
                    AuditOutcome.Denied,
                    RecordCodeReasonCodes.Forbidden,
                    2,
                    null,
                    null),
                cancellationToken);
            if (!written)
            {
                _logger.LogWarning("Record-code platform-actor refusal record was not inserted.");
            }
        }
        catch (Exception exception) when (exception is TransactionOwnedAuditRefusedException or PlatformTransactionUnavailableException)
        {
            _logger.LogWarning(exception, "Record-code platform-actor refusal record could not be written; the answer stays 403.");
        }
    }

    private IActionResult Refused(
        Func<int, string, IActionResult> failure,
        int statusCode,
        string reasonCode,
        string? sourceService,
        Guid? tenantId)
    {
        _logger.LogWarning(
            "Record-code allocation refused at the gate: {ReasonCode}; source service {SourceService}, tenant {TenantId}.",
            reasonCode, sourceService, tenantId);
        return failure(statusCode, reasonCode);
    }

    /// <summary>The body, read once and never past 4 KB + 1 byte; null when it is larger (a chunked body has no length).</summary>
    private static async Task<byte[]?> ReadBoundedBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
