using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.ESignature.Services;

// ── AuthService ticket consume (E1 internal endpoint, CT-D9) ────────────────────────────────────────────────────────

public enum TicketConsumeOutcome
{
    Consumed = 0,

    /// <summary>AuthService answered 409 ESIGN_TICKET_INVALID: unknown / expired / spent / other user, tenant or intent.</summary>
    Invalid = 1,

    /// <summary>AuthService unreachable, unconfigured, or answered anything else: fail closed (503).</summary>
    Unavailable = 2
}

public sealed record TicketConsumeResult(
    TicketConsumeOutcome Outcome,
    ESignatureReauthKind Kind = ESignatureReauthKind.Password,
    DateTime PasswordVerifiedAtUtc = default,
    DateTime IssuedAtUtc = default)
{
    public static readonly TicketConsumeResult Invalid = new(TicketConsumeOutcome.Invalid);
    public static readonly TicketConsumeResult Unavailable = new(TicketConsumeOutcome.Unavailable);
}

/// <summary>
/// Platform → AuthService <c>POST internal/esign-tickets/consume</c> (E1; ESIGN-BUNDLE §4.3). The call carries no user
/// authority: the user and tenant come from the caller's own token, the ticket from the caller's own re-authentication.
/// </summary>
public interface IESignTicketConsumer
{
    Task<TicketConsumeResult> ConsumeAsync(string ticket, Guid tenantId, Guid userId, string intent, CancellationToken ct);
}

/// <summary>The ticket is never stored: records keep its SHA-256 so a spent re-authentication is traceable, not reusable.</summary>
public static class ESignTicketRef
{
    public static string Of(string ticket) => ESignatureDigest.Sha256Hex("esign-ticket:" + ticket);
}

// ── who is signing (JWT sub, never the body) ────────────────────────────────────────────────────────────────────────

public sealed record ESignActor(Guid TenantId, Guid UserId, string UserIdText, string Login, string Name, string Language);

public interface IESignActorAccessor
{
    /// <summary>The authenticated tenant user, or null for anyone else (platform / partner admins, anonymous).</summary>
    ESignActor? Current();
}

public sealed class ESignActorAccessor : IESignActorAccessor
{
    private readonly ITenantAuthorizationContext _authorization;
    private readonly ICurrentUserContext _currentUser;
    private readonly IESignRequestLanguage _language;

    public ESignActorAccessor(ITenantAuthorizationContext authorization, ICurrentUserContext currentUser, IESignRequestLanguage language)
    {
        _authorization = authorization;
        _currentUser = currentUser;
        _language = language;
    }

    public ESignActor? Current()
    {
        // Rule 1 of ESIGN-BUNDLE: every call is made with the user's own tenant token. A platform or partner administrator
        // passes [HasPermission] by bypass, but may not sign (§11.200(a)(2) — only the genuine owner).
        if (!_authorization.IsAuthenticated
            || !string.Equals(_authorization.ActorType, "tenant_user", StringComparison.Ordinal)
            || _authorization.TenantId == Guid.Empty
            || _authorization.UserId == Guid.Empty)
        {
            return null;
        }

        var login = _currentUser.Email ?? _authorization.UserId.ToString("D");
        var name = _currentUser.DisplayName ?? _currentUser.Email ?? login;
        return new ESignActor(_authorization.TenantId, _authorization.UserId, _authorization.UserId.ToString("D"), login, name, _language.Current());
    }
}

/// <summary>The signer's UI language (Accept-Language), used for <c>MeaningTextAtSign</c>.</summary>
public interface IESignRequestLanguage
{
    string Current();
}

// ── audit intent inside the business transaction ────────────────────────────────────────────────────────────────────

/// <summary>
/// MOD-0021 audit for MOD-0022 commands, written as an outbox intent IN the business transaction (ESIGN-BUNDLE rule 7,
/// prompt "imza kaydı + talep durumu + denetim tek işlemde"). The payload has the full shape the outbox relay maps into
/// <c>audit_events</c> (same keys as <c>AuditService.BuildPayload</c>). Metadata never carries a ticket, a password or
/// image bytes (rule 8) — callers pass identifiers, codes and hashes only.
/// </summary>
public static class ESignatureAuditIntent
{
    public static async Task EnqueueAsync(
        ITransactionalAuditOutboxWriter writer,
        IPlatformTransactionSession session,
        Guid tenantId,
        string eventName,
        AuditOperation operation,
        string entityType,
        Guid? entityId,
        AuditActorType actorType,
        Guid? actorId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken ct)
    {
        var correlationId = Guid.NewGuid();
        var payload = new Dictionary<string, object?>
        {
            ["TenantId"] = tenantId,
            ["CorrelationId"] = correlationId,
            ["RequestType"] = eventName,
            ["ActorType"] = actorType.ToString(),
            ["ActorId"] = actorId,
            ["Category"] = AuditCategory.ESignature.ToString(),
            ["EntityType"] = entityType,
            ["EntityId"] = entityId,
            ["Operation"] = operation.ToString(),
            ["Outcome"] = AuditOutcome.Succeeded.ToString(),
            ["Metadata"] = metadata.ToDictionary(p => p.Key, p => p.Value),
            ["OccurredAtUtc"] = DateTimeOffset.UtcNow,
            ["SourceService"] = "Diten.Platform",
            ["SourceModule"] = ESignAuditEvents.SourceModule,
            ["IsMetaAudit"] = false,
            ["RedactionStatus"] = AuditRedactionStatus.None.ToString()
        };

        var inserted = await writer.TryEnqueueAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = tenantId,
            CorrelationId = correlationId,
            IdempotencyKey = $"esign:{eventName}:{entityId:N}:{correlationId:N}",
            RequestType = eventName,
            Operation = operation,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload
        }, ct);
        if (!inserted)
        {
            throw new PlatformTransactionUnavailableException("The e-signature audit intent was not inserted.");
        }
    }
}

// ── tenant catalog: seeded meanings, the tenant-default policy, effective rows ───────────────────────────────────────

public interface IESignatureCatalog
{
    /// <summary>Seeds APPROVE / REVIEW / RELEASE / AUTHOR on a tenant's first read (pack §4 "Seeded per tenant").</summary>
    Task EnsureMeaningsAsync(Guid tenantId, CancellationToken ct);

    /// <summary>The tenant default; created from catalog defaults on first read (pack §8 policy).</summary>
    Task<ESignaturePolicy> GetTenantPolicyAsync(Guid tenantId, CancellationToken ct);

    /// <summary>KS-5: the override for <paramref name="legalEntityId"/> when there is one, else the tenant default.</summary>
    Task<ESignaturePolicy> GetEffectivePolicyAsync(Guid tenantId, Guid? legalEntityId, CancellationToken ct);

    /// <summary>The policy row for an action; when the policy predates the action's registration, the catalog defaults.</summary>
    Task<ESignaturePolicyRow?> GetRowAsync(Guid tenantId, ESignaturePolicy policy, string? actionCode, CancellationToken ct);
}

public sealed class ESignatureCatalog : IESignatureCatalog
{
    internal static readonly IReadOnlyList<(string Code, Dictionary<string, string> Names)> SeedMeanings =
    [
        ("APPROVE", new() { ["en"] = "I approve", ["tr"] = "Onaylıyorum", ["fr"] = "J'approuve", ["es"] = "Apruebo", ["zh"] = "我批准", ["ar"] = "أوافق", ["ru"] = "Утверждаю" }),
        ("REVIEW", new() { ["en"] = "I have reviewed", ["tr"] = "İnceledim", ["fr"] = "J'ai examiné", ["es"] = "He revisado", ["zh"] = "我已审阅", ["ar"] = "لقد راجعت", ["ru"] = "Проверено мной" }),
        ("RELEASE", new() { ["en"] = "I authorize release", ["tr"] = "Serbest bırakma yetkisi", ["fr"] = "J'autorise la libération", ["es"] = "Autorizo la liberación", ["zh"] = "我授权放行", ["ar"] = "أأذن بالإفراج", ["ru"] = "Разрешаю выпуск" }),
        ("AUTHOR", new() { ["en"] = "I am the author", ["tr"] = "Hazırlayan benim", ["fr"] = "Je suis l'auteur", ["es"] = "Soy el autor", ["zh"] = "我是作者", ["ar"] = "أنا المؤلف", ["ru"] = "Я автор" })
    ];

    private readonly IESignMeaningRepository _meanings;
    private readonly IESignPolicyRepository _policies;
    private readonly IESignActionRepository _actions;

    public ESignatureCatalog(IESignMeaningRepository meanings, IESignPolicyRepository policies, IESignActionRepository actions)
    {
        _meanings = meanings;
        _policies = policies;
        _actions = actions;
    }

    public async Task EnsureMeaningsAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await _meanings.ListAsync(tenantId, activeOnly: false, ct);
        if (existing.Count > 0)
        {
            return;
        }

        foreach (var (code, names) in SeedMeanings)
        {
            // Duplicate-key safe: two first reads racing both try, one wins per code.
            await _meanings.InsertAsync(null, new ESignatureMeaning
            {
                TenantId = tenantId,
                Code = code,
                Names = new Dictionary<string, string>(names),
                IsActive = true,
                CreatedBy = "system:esign-seed"
            }, ct);
        }
    }

    public async Task<ESignaturePolicy> GetTenantPolicyAsync(Guid tenantId, CancellationToken ct)
    {
        var policy = await _policies.GetTenantAsync(tenantId, ct);
        if (policy is not null)
        {
            return policy;
        }

        var actions = await _actions.ListAsync(ct);
        var seeded = new ESignaturePolicy
        {
            TenantId = tenantId,
            Scope = ESignaturePolicyScope.Tenant,
            Rows = actions.Select(DefaultRow).ToList(),
            CreatedBy = "system:esign-seed"
        };
        await _policies.InsertAsync(null, seeded, ct);
        return await _policies.GetTenantAsync(tenantId, ct) ?? seeded;
    }

    public async Task<ESignaturePolicy> GetEffectivePolicyAsync(Guid tenantId, Guid? legalEntityId, CancellationToken ct)
    {
        var tenantDefault = await GetTenantPolicyAsync(tenantId, ct);
        var legalEntityOverride = legalEntityId is { } id ? await _policies.GetLegalEntityAsync(tenantId, id, ct) : null;
        return ESignatureRules.Effective(tenantDefault, legalEntityOverride);
    }

    public async Task<ESignaturePolicyRow?> GetRowAsync(Guid tenantId, ESignaturePolicy policy, string? actionCode, CancellationToken ct)
    {
        var row = ESignatureRules.Row(policy, actionCode);
        if (row is not null || string.IsNullOrWhiteSpace(actionCode))
        {
            return row;
        }

        var action = await _actions.GetAsync(actionCode, ct);
        return action is null ? null : DefaultRow(action);
    }

    internal static ESignaturePolicyRow DefaultRow(ESignableAction action) => new()
    {
        ActionCode = action.ActionCode,
        Required = action.DefaultRequired,
        MeaningCode = action.DefaultMeaningCode,
        VisualMode = action.DefaultVisualMode,
        SecondFactor = ESignatureSecondFactor.NotAvailable
    };
}

// ── list / export visibility (pack §8 "Visibility and company", KS-2) ───────────────────────────────────────────────

/// <summary>
/// The legal entities whose signatures the caller may list or export: the SEC-002 data scope's LegalEntity includes
/// (<see cref="IDataScopeResolver"/>, module <c>esign</c>). An empty answer means "own signatures only" — fail closed.
/// </summary>
public interface IESignatureVisibility
{
    Task<IReadOnlyCollection<Guid>> VisibleLegalEntitiesAsync(ESignActor actor, CancellationToken ct);
}

public sealed class ESignatureVisibility : IESignatureVisibility
{
    private readonly IDataScopeResolver _scopes;

    public ESignatureVisibility(IDataScopeResolver scopes) => _scopes = scopes;

    public async Task<IReadOnlyCollection<Guid>> VisibleLegalEntitiesAsync(ESignActor actor, CancellationToken ct)
    {
        var scopes = await _scopes.ResolveAsync(actor.TenantId, actor.UserId, "esign", "signatures", ct);
        return scopes
            .Where(s => s.Kind == EntitlementDataScopeKind.LegalEntity && s.IsInclude && s.ScopeId is { } id && id != Guid.Empty)
            .Select(s => s.ScopeId!.Value)
            .Distinct()
            .ToList();
    }
}
