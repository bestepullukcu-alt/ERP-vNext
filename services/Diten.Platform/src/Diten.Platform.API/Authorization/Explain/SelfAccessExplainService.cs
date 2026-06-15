using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.API.Authorization.Explain;

/// <inheritdoc />
public sealed class SelfAccessExplainService : ISelfAccessExplainService
{
    private const string SelfMode = "self";
    private const string SourceServiceName = "Diten.Platform";
    private const string SourceModuleCode = "MOD-0018";
    private const string ExplainRequestType = "authorization.explain.self";
    private const string ExplainEntityType = "AccessExplain";
    private const string PlatformAdminActorType = "platform_admin";
    private const string PartnerAdminActorType = "partner_admin";
    private const string TenantUserActorType = "tenant_user";

    // Bounded scope-observation notes. The data-scope observation is DESCRIPTIVE only — never combined with the
    // permission observation into an Allowed verdict (data-scope is opt-in per resource and platform/partner admins are
    // not row-scoped, so an empty scope does not imply a universal deny; there is also no permission↔module binding).
    private const string ScopeOptInNote = "data-scope-opt-in-per-resource";
    private const string ScopeApplicabilityNote = "scope-applicability-not-determined";
    private const string EmptyScopeNote = "empty-scope-does-not-imply-universal-deny";
    private const string ScopeFailedNote = "scope-observation-failed";

    // Static, bounded freshness notes. FU14 v1 deliberately cannot prove token-version / revocation / cache state.
    private static readonly IReadOnlyList<string> FreshnessNotesValue =
    [
        "refresh-required-after-grant-change",
        "token-version-unavailable",
        "revocation-timestamp-unavailable",
    ];

    private readonly ITenantAuthorizationContext _authContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDataScopeResolver _dataScopeResolver;
    private readonly IAuditService _auditService;
    private readonly ICorrelationContext _correlationContext;
    private readonly ILogger<SelfAccessExplainService> _logger;

    public SelfAccessExplainService(
        ITenantAuthorizationContext authContext,
        ICurrentUserContext currentUser,
        IDataScopeResolver dataScopeResolver,
        IAuditService auditService,
        ICorrelationContext correlationContext,
        ILogger<SelfAccessExplainService> logger)
    {
        _authContext = authContext;
        _currentUser = currentUser;
        _dataScopeResolver = dataScopeResolver;
        _auditService = auditService;
        _correlationContext = correlationContext;
        _logger = logger;
    }

    public async Task<Response<SelfAccessExplainResponse>> ExplainAsync(
        ClaimsPrincipal principal,
        string? permissionKey,
        string? moduleCode,
        string? featureCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Self subject — ONLY from the authenticated context; never from the request. Read first so the application-level
        // deny paths below can emit a bounded audit with the (safe) actor + tenant.
        var tenantId = _authContext.TenantId;
        var userId = _authContext.UserId;
        var actorType = _authContext.ActorType;

        // Bounded request validation — NOT a PKS-001 catalog / known-key check. A validation deny still reaches the
        // service, so it is audited (bounded; the raw invalid input is never written to metadata).
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            await AuditValidationDenyAsync("permission-key-required", actorType, tenantId, cancellationToken);
            return Response<SelfAccessExplainResponse>.Fail("permissionKey is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(moduleCode))
        {
            await AuditValidationDenyAsync("module-code-required", actorType, tenantId, cancellationToken);
            return Response<SelfAccessExplainResponse>.Fail("moduleCode is required.", 400);
        }

        // Fail-closed: the self route needs a usable authenticated subject from the context (never the request).
        if (!_authContext.IsAuthenticated || userId == Guid.Empty || tenantId == Guid.Empty)
        {
            await AuditValidationDenyAsync("authenticated-context-invalid", actorType, tenantId, cancellationToken);
            return Response<SelfAccessExplainResponse>.Fail("Authenticated context could not be resolved.", 403);
        }

        var normalizedFeatureCode = string.IsNullOrWhiteSpace(featureCode) ? null : featureCode;

        // Permission observation — side-effect free; the real HasPermissionAttribute filter is never invoked.
        var (permissionMatch, matchedViaLegacyAlias, permissionSatisfied) =
            ObservePermission(principal, actorType, permissionKey);

        // Data-scope observation — read-only reuse; project kind + count only (raw scope ids never leave the service).
        // This is DESCRIPTIVE: it is NOT combined with the permission observation into an Allowed verdict. Data-scope is
        // opt-in per resource and platform/partner admins are not row-scoped (BME-001), so an empty scope does not imply
        // a universal deny — and there is no permission↔module binding catalog to soundly combine the two inputs.
        var scopeKinds = new List<string>();
        var scopeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var scopeNotes = new List<string> { ScopeOptInNote, ScopeApplicabilityNote };
        var diagnosticFailure = false;

        try
        {
            var scopes = await _dataScopeResolver.ResolveAsync(tenantId, userId, moduleCode, normalizedFeatureCode, cancellationToken);
            foreach (var scope in scopes)
            {
                var kind = scope.Kind.ToString();
                if (scopeCounts.TryGetValue(kind, out var count))
                {
                    scopeCounts[kind] = count + 1;
                }
                else
                {
                    scopeKinds.Add(kind);
                    scopeCounts[kind] = 1;
                }
            }

            if (scopeCounts.Count == 0)
            {
                scopeNotes.Add(EmptyScopeNote);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A resolver failure is a diagnostic — it never overrides the permission observation, never opens/closes
            // access, and the raw exception is never returned.
            _logger.LogWarning(ex, "Data-scope resolution failed during self-access explain. ErrorType={ErrorType}", ex.GetType().Name);
            diagnosticFailure = true;
            scopeKinds.Clear();
            scopeCounts.Clear();
            scopeNotes.Add(ScopeFailedNote);
        }

        var response = new SelfAccessExplainResponse(
            Mode: SelfMode,
            PermissionSatisfied: permissionSatisfied,
            RequiredPermission: permissionKey,
            PermissionMatch: permissionMatch,
            MatchedViaLegacyAlias: matchedViaLegacyAlias,
            ActorType: actorType,
            TenantId: tenantId,
            ScopeKinds: scopeKinds,
            ScopeCounts: scopeCounts,
            ScopeNotes: scopeNotes,
            TokenExpiresAtUtc: ReadTokenExpiry(principal),
            FreshnessNotes: FreshnessNotesValue,
            DiagnosticFailure: diagnosticFailure);

        await AuditAsync(response, tenantId, actorType, diagnosticFailure, cancellationToken);

        return Response<SelfAccessExplainResponse>.Success(response, 200);
    }

    private static (string match, bool matchedViaLegacyAlias, bool satisfied) ObservePermission(
        ClaimsPrincipal principal, string? actorType, string permissionKey)
    {
        // Mirror HasPermissionAttribute's actor bypass WITHOUT its admin-lifecycle side effect (read-only observation).
        if (string.Equals(actorType, PlatformAdminActorType, StringComparison.OrdinalIgnoreCase))
        {
            return ("bypass-platform-admin", false, true);
        }

        if (string.Equals(actorType, PartnerAdminActorType, StringComparison.OrdinalIgnoreCase))
        {
            return ("bypass-partner-admin", false, true);
        }

        var evaluation = PermissionClaimEvaluator.Evaluate(principal.Claims, permissionKey);
        var match = evaluation.Match switch
        {
            PermissionClaimMatch.CanonicalMatch => "canonical",
            PermissionClaimMatch.LegacyAliasMatch => "legacy-alias",
            _ => "missing",
        };
        return (match, evaluation.MatchedViaLegacyAlias, evaluation.IsSatisfied);
    }

    private static DateTimeOffset? ReadTokenExpiry(ClaimsPrincipal principal)
    {
        var exp = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        return long.TryParse(exp, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }

    private Task AuditAsync(
        SelfAccessExplainResponse response,
        Guid tenantId,
        string? actorType,
        bool diagnosticFailure,
        CancellationToken cancellationToken)
    {
        // Outcome reflects the permission-gate observation (the real access gate), never the removed combined verdict.
        var outcome = diagnosticFailure
            ? AuditOutcome.Failed
            : response.PermissionSatisfied ? AuditOutcome.Succeeded : AuditOutcome.Denied;

        // Bounded metadata only — primitive/string values, no raw claim / alias / scope id / exception text.
        var metadata = new Dictionary<string, object?>
        {
            ["mode"] = response.Mode,
            ["requiredPermission"] = response.RequiredPermission,
            ["permissionSatisfied"] = response.PermissionSatisfied,
            ["permissionMatch"] = response.PermissionMatch,
            ["matchedViaLegacyAlias"] = response.MatchedViaLegacyAlias,
            ["actorType"] = response.ActorType,
            ["tenantId"] = response.TenantId,
            ["scopeKinds"] = string.Join(",", response.ScopeKinds),
            ["scopeCounts"] = string.Join(",", response.ScopeCounts.Select(kv => $"{kv.Key}:{kv.Value}")),
            ["scopeNotes"] = string.Join(",", response.ScopeNotes),
            ["diagnosticFailure"] = response.DiagnosticFailure,
        };

        return AppendAuditAsync(outcome, actorType, tenantId, metadata, cancellationToken);
    }

    private Task AuditValidationDenyAsync(
        string validationCode,
        string? actorType,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // A validation deny reaching the service is an application-level deny → audited. Bounded metadata only — the
        // raw invalid input (the blank/whitespace value the caller sent) is NEVER written.
        var metadata = new Dictionary<string, object?>
        {
            ["mode"] = SelfMode,
            ["permissionSatisfied"] = false,
            ["diagnosticFailure"] = false,
            ["validationCode"] = validationCode,
            ["actorType"] = actorType,
            ["tenantId"] = tenantId == Guid.Empty ? null : tenantId,
        };

        return AppendAuditAsync(AuditOutcome.Denied, actorType, tenantId, metadata, cancellationToken);
    }

    private async Task AppendAuditAsync(
        AuditOutcome outcome,
        string? actorType,
        Guid tenantId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        var request = new AuditAppendRequest
        {
            CorrelationId = ResolveCorrelationId(),
            RequestType = ExplainRequestType,
            ActorType = MapActorType(actorType),
            ActorId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId,
            ActorEmail = _currentUser.Email,
            ActorDisplayName = _currentUser.DisplayName,
            TargetTenantId = tenantId == Guid.Empty ? null : tenantId,
            Category = AuditCategory.Security,
            EntityType = ExplainEntityType,
            Operation = AuditOperation.Execute,
            Outcome = outcome,
            SourceService = SourceServiceName,
            SourceModule = SourceModuleCode,
            Metadata = metadata,
        };

        try
        {
            await _auditService.AppendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // An audit-write failure NEVER changes the explain result or any authorization decision.
            _logger.LogWarning(ex, "Self-access explain audit append failed. ErrorType={ErrorType}", ex.GetType().Name);
        }
    }

    private Guid ResolveCorrelationId() =>
        Guid.TryParse(_correlationContext.CorrelationId, out var correlationId) ? correlationId : Guid.NewGuid();

    private static AuditActorType MapActorType(string? actorType) => actorType?.ToLowerInvariant() switch
    {
        PlatformAdminActorType => AuditActorType.PlatformAdministrator,
        PartnerAdminActorType => AuditActorType.PartnerAdministrator,
        TenantUserActorType => AuditActorType.TenantUser,
        _ => AuditActorType.TenantUser,
    };
}
