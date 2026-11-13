using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0262-FU01 DL-2 — the refusal and cancellation record writer of the trusted document repository surface (§8.1
/// "Ret başına tek yazar", §21). It writes through the central door (<see cref="IAuditService"/>; emsal
/// <c>PartnerAdminScopeAttribute.RecordAsync</c>) and fills the actor fields itself, never null (r5, F-2): otherwise
/// <c>AuditService</c> would fill them from <c>ICurrentUserContext</c> — a user JWT validated by the default scheme
/// would put a person into a service surface's refusal.
/// <list type="bullet">
/// <item><b>Unauthenticated</b> (header pre-check 401 / 403 / 413, <c>AuthenticateAsync</c> 401, claim defence 403):
/// platform-global (<c>PlatformSystemTenantId</c>), <c>ActorType = System</c>, actor id empty, email empty, name
/// <c>"system"</c>; nothing of the unverified token is written. Limited to 60 records a minute (fixed window, one
/// partition): a refusal over the limit is answered the same, NOT recorded, logged as a warning and counted on
/// <c>document_repository_trusted_refusals_unrecorded_total{reason="rate_limited"}</c> — the named departure from AUD-001
/// §3 (owner Beste).</item>
/// <item><b>Authenticated</b> (closed list / scope 403, multipart and validator 400, 404): the token's tenant — the caller
/// opens <c>TenantScope</c> — <c>ActorType = Service</c>, <c>ActorId = ClientId</c>, email and name empty. Not limited.</item>
/// <item><b>Client aborted</b>: as authenticated, <c>Outcome = Failed</c>, the command's operation, written with
/// <see cref="CancellationToken.None"/> (the request's token is already cancelled).</item>
/// </list>
/// A record that cannot be written never changes the answer (§13).
/// </summary>
public sealed class TrustedDocumentRepositoryRefusalAudit
{
    public const string DeniedRequestType = "document-repository.trusted-consumer.denied";
    public const string ClientAbortedRequestType = "document-repository.trusted-consumer.client-aborted";
    public const string MeterName = "Diten.Platform.DocumentRepository.TrustedConsumer";
    public const string UnrecordedCounterName = "document_repository_trusted_refusals_unrecorded_total";
    public const string RateLimitedReason = "rate_limited";
    public const int UnauthenticatedPermitsPerMinute = 60;

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Unrecorded = Meter.CreateCounter<long>(
        UnrecordedCounterName,
        description: "Unauthenticated trusted document repository refusals not written to the audit trail (rate limited).");

    private readonly IAuditService _audit;
    private readonly UnauthenticatedLimiter _unauthenticated;
    private readonly ILogger<TrustedDocumentRepositoryRefusalAudit> _logger;

    public TrustedDocumentRepositoryRefusalAudit(
        IAuditService audit,
        UnauthenticatedLimiter unauthenticated,
        ILogger<TrustedDocumentRepositoryRefusalAudit> logger)
    {
        _audit = audit;
        _unauthenticated = unauthenticated;
        _logger = logger;
    }

    /// <summary>
    /// The process-wide fixed window (60 a minute, queue 0) of unauthenticated refusal records — a singleton, while the
    /// writer itself is scoped (it writes through the scoped <see cref="IAuditService"/>).
    /// </summary>
    public sealed class UnauthenticatedLimiter : IDisposable
    {
        private readonly FixedWindowRateLimiter _limiter = new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = UnauthenticatedPermitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });

        public bool TryAcquire()
        {
            using var lease = _limiter.AttemptAcquire();
            return lease.IsAcquired;
        }

        public void Dispose() => _limiter.Dispose();
    }

    /// <summary>An unauthenticated refusal: nothing unverified is written; limited to 60 a minute.</summary>
    public async Task RecordUnauthenticatedAsync(
        string endpoint, string reasonCode, AuditOutcome outcome, Guid correlationId, CancellationToken ct)
    {
        if (!_unauthenticated.TryAcquire())
        {
            Unrecorded.Add(1, new KeyValuePair<string, object?>("reason", RateLimitedReason));
            _logger.LogWarning(
                "Trusted document repository refusal {ReasonCode} on {Endpoint} was not recorded: more than {Limit} " +
                "unauthenticated refusals this minute (AUD-001 §3 named departure).",
                reasonCode, endpoint, UnauthenticatedPermitsPerMinute);
            return;
        }

        await AppendAsync(new AuditAppendRequest
        {
            CorrelationId = correlationId,
            RequestType = DeniedRequestType,
            ActorType = AuditActorType.System,
            ActorId = Guid.Empty,
            ActorEmail = string.Empty,
            ActorDisplayName = "system",
            Category = AuditCategory.Security,
            EntityType = "RepositoryObject",
            Operation = AuditOperation.PermissionDenied,
            Outcome = outcome,
            Metadata = new Dictionary<string, object?>
            {
                ["ReasonCode"] = reasonCode,
                ["Endpoint"] = endpoint
            },
            SourceService = "Diten.Platform",
            SourceModule = "MOD-0262-FU01",
            IsPlatformGlobal = true
        }, ct);
    }

    /// <summary>An authenticated refusal, under the token's tenant (the caller has opened <c>TenantScope</c>).</summary>
    public Task RecordAuthenticatedAsync(
        TrustedDocumentRepositoryConsumerIdentity identity,
        string endpoint,
        string reasonCode,
        AuditOutcome outcome,
        Guid? contentId,
        Guid correlationId,
        CancellationToken ct) =>
        AppendAsync(new AuditAppendRequest
        {
            CorrelationId = correlationId,
            RequestType = DeniedRequestType,
            ActorType = AuditActorType.Service,
            ActorId = identity.ClientId,
            ActorEmail = string.Empty,
            ActorDisplayName = string.Empty,
            Category = AuditCategory.Security,
            EntityType = "RepositoryObject",
            EntityId = contentId,
            Operation = AuditOperation.PermissionDenied,
            Outcome = outcome,
            Metadata = new Dictionary<string, object?>
            {
                ["ReasonCode"] = reasonCode,
                ["Endpoint"] = endpoint,
                ["serviceClientId"] = identity.ClientId.ToString("D")
            },
            SourceService = "Diten.Platform",
            SourceModule = "MOD-0262-FU01"
        }, ct);

    /// <summary>
    /// The client went away while the command ran (§8.1 Dal 1). Written with <see cref="CancellationToken.None"/>. Means
    /// "the client got no answer; the outcome is not decided by this record" — the row is the outcome.
    /// </summary>
    public Task RecordClientAbortedAsync(
        TrustedDocumentRepositoryConsumerIdentity identity,
        string endpoint,
        AuditOperation operation,
        Guid? contentId,
        Guid correlationId) =>
        AppendAsync(new AuditAppendRequest
        {
            CorrelationId = correlationId,
            RequestType = ClientAbortedRequestType,
            ActorType = AuditActorType.Service,
            ActorId = identity.ClientId,
            ActorEmail = string.Empty,
            ActorDisplayName = string.Empty,
            Category = AuditCategory.DocumentManagement,
            EntityType = "RepositoryObject",
            EntityId = contentId,
            Operation = operation,
            Outcome = AuditOutcome.Failed,
            Metadata = new Dictionary<string, object?>
            {
                ["ReasonCode"] = Application.Contracts.DocumentRepository.DocumentRepositoryReasonCodes.TrustedConsumerClientAborted,
                ["Endpoint"] = endpoint,
                ["serviceClientId"] = identity.ClientId.ToString("D")
            },
            SourceService = "Diten.Platform",
            SourceModule = "MOD-0262-FU01"
        }, CancellationToken.None);

    private async Task AppendAsync(AuditAppendRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _audit.AppendAsync(request, ct);
            if (result.Status != AuditAppendStatus.Queued)
            {
                _logger.LogWarning(
                    "Trusted document repository record {RequestType} was not written (status {Status}); the answer stands.",
                    request.RequestType, result.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Trusted document repository record {RequestType} could not be written; the answer stands.",
                request.RequestType);
        }
    }
}
