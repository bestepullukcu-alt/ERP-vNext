using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;

/// <summary>
/// MOD-0029-FU04 — read-time effective access resolver. Deterministic rule, per action:
/// gather every applicable decision (matrix policy mentioning the action, plus compatibility folder grants) across
/// the target's ancestry; take the decision at the NEAREST ancestor distance; at that distance a Deny beats an
/// Allow (deny precedence). No decision anywhere ⇒ default deny. Disabled/Archived, out-of-window and
/// non-inheriting ancestor policies are ignored. Tenant isolation is provided by the tenant-scoped repository.
/// </summary>
public sealed class DocumentAccessResolver
{
    private readonly IDocumentAccessPolicyRepository _policies;
    private readonly DocumentAccessInheritanceResolver _inheritance;
    private readonly DocumentAccessCompatibilityAdapter _compatibility;
    private readonly IDocumentAccessPrincipalAccessor _principalAccessor;
    private readonly AccessMatrixOptions _options;

    public DocumentAccessResolver(
        IDocumentAccessPolicyRepository policies,
        DocumentAccessInheritanceResolver inheritance,
        DocumentAccessCompatibilityAdapter compatibility,
        IDocumentAccessPrincipalAccessor principalAccessor,
        IOptions<AccessMatrixOptions> options)
    {
        _policies = policies;
        _inheritance = inheritance;
        _compatibility = compatibility;
        _principalAccessor = principalAccessor;
        _options = options.Value;
    }

    public sealed record PrincipalIdentity(DocumentAccessPrincipalType Type, string Id);

    private sealed record Decision(int Distance, DocumentAccessEffect Effect, DocumentAccessTargetType SourceType);

    /// <summary>Effective access for an explicit principal (preview).</summary>
    public Task<EffectiveDocumentAccessModel> ResolveAsync(
        DocumentAccessTargetType targetType,
        string targetId,
        DocumentAccessPrincipalType principalType,
        string principalId,
        CancellationToken ct)
    {
        var identities = new List<PrincipalIdentity> { new(principalType, principalId.Trim()) };
        var tokens = TokensFor(principalType, principalId);
        return ResolveCoreAsync(targetType, targetId, identities, tokens, principalType.ToWire(), principalId.Trim(), ct);
    }

    /// <summary>Effective access for the current principal (enforcement). Expands user + roles + companies.</summary>
    public Task<EffectiveDocumentAccessModel> ResolveForCurrentAsync(
        DocumentAccessTargetType targetType,
        string targetId,
        CancellationToken ct)
    {
        var principal = _principalAccessor.GetPrincipal();
        var identities = new List<PrincipalIdentity>();
        if (principal.UserId != Guid.Empty) identities.Add(new(DocumentAccessPrincipalType.User, principal.UserId.ToString("D")));
        foreach (var role in principal.RoleIds) if (!string.IsNullOrWhiteSpace(role)) identities.Add(new(DocumentAccessPrincipalType.Role, role.Trim()));
        foreach (var company in principal.CompanyIds) if (company != Guid.Empty) identities.Add(new(DocumentAccessPrincipalType.Company, company.ToString("D")));

        return ResolveCoreAsync(targetType, targetId, identities, principal.GranteeTokens(),
            DocumentAccessPrincipalType.User.ToWire(), principal.UserId.ToString("D"), ct);
    }

    /// <summary>Convenience for enforcement gates: does the current principal hold the action on the target?</summary>
    public async Task<bool> CurrentHasActionAsync(
        DocumentAccessTargetType targetType,
        Guid targetId,
        DocumentAccessMatrixAction action,
        CancellationToken ct)
    {
        if (_principalAccessor.GetPrincipal().HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var effective = await ResolveForCurrentAsync(targetType, targetId.ToString("D"), ct);
        return effective.AllowedActions.Contains(action.ToWire());
    }

    public async Task<DocumentAccessDecision> ResolveCurrentDecisionAsync(
        DocumentAccessTargetType targetType,
        Guid targetId,
        DocumentAccessMatrixAction action,
        CancellationToken ct)
    {
        if (_principalAccessor.GetPrincipal().HasAdministrativeDocumentAccess)
        {
            return DocumentAccessDecision.Allow;
        }

        var effective = await ResolveForCurrentAsync(targetType, targetId.ToString("D"), ct);
        var wire = action.ToWire();
        var decision = effective.Decisions.FirstOrDefault(d => string.Equals(d.Action, wire, StringComparison.OrdinalIgnoreCase));
        if (decision is null)
        {
            return DocumentAccessDecision.NoDecision;
        }

        return decision.Allowed ? DocumentAccessDecision.Allow : DocumentAccessDecision.Deny;
    }

    private async Task<EffectiveDocumentAccessModel> ResolveCoreAsync(
        DocumentAccessTargetType targetType,
        string targetId,
        IReadOnlyList<PrincipalIdentity> identities,
        IReadOnlySet<string> granteeTokens,
        string principalTypeWire,
        string principalIdWire,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var ancestors = await _inheritance.BuildAncestorsAsync(targetType, targetId, ct);
        var distanceByKey = ancestors.ToDictionary(a => Key(a.TargetType, a.TargetId), a => a, StringComparer.OrdinalIgnoreCase);

        var policies = await _policies.GetByTargetsAsync(
            ancestors.Select(a => (a.TargetType, a.TargetId)).ToList(), ct);

        // action -> decisions
        var decisions = new Dictionary<DocumentAccessMatrixAction, List<Decision>>();
        void AddDecision(DocumentAccessMatrixAction action, int distance, DocumentAccessEffect effect, DocumentAccessTargetType source)
        {
            if (!decisions.TryGetValue(action, out var list)) { list = []; decisions[action] = list; }
            list.Add(new Decision(distance, effect, source));
        }

        foreach (var policy in policies)
        {
            if (policy.Status != DocumentAccessPolicyStatus.Active) continue;
            if (policy.ValidFrom is { } from && from > now) continue;
            if (policy.ValidTo is { } to && to < now) continue;
            if (!distanceByKey.TryGetValue(Key(policy.TargetType, policy.TargetId), out var ancestor)) continue;
            if (ancestor.Distance > 0 && !policy.InheritFromParent) continue;
            if (!MatchesPrincipal(policy, identities)) continue;

            foreach (var action in policy.Actions.Distinct())
            {
                AddDecision(action, ancestor.Distance, policy.Effect, policy.TargetType);
            }
        }

        // Compatibility: existing folder grants count as Allow at the folder ancestor distance (transitional rollout).
        foreach (var ancestor in ancestors.Where(a => a.TargetType == DocumentAccessTargetType.CollectionInstance))
        {
            if (!Guid.TryParse(ancestor.TargetId, out var folderId)) continue;
            var compat = await _compatibility.FolderActionsAsync(folderId, granteeTokens, ct);
            foreach (var action in compat)
            {
                AddDecision(action, ancestor.Distance, DocumentAccessEffect.Allow, DocumentAccessTargetType.CollectionInstance);
            }
        }

        var allowed = new List<string>();
        var explained = new List<EffectiveActionModel>();
        foreach (var (action, list) in decisions)
        {
            var nearest = list.Min(d => d.Distance);
            var atNearest = list.Where(d => d.Distance == nearest).ToList();
            var effect = atNearest.Any(d => d.Effect == DocumentAccessEffect.Deny) ? DocumentAccessEffect.Deny : DocumentAccessEffect.Allow;
            var source = atNearest.First(d => d.Effect == effect).SourceType;
            var isAllowed = effect == DocumentAccessEffect.Allow;
            if (isAllowed) allowed.Add(action.ToWire());
            explained.Add(new EffectiveActionModel(action.ToWire(), isAllowed, effect.ToWire(), source.ToWire(), nearest > 0));
        }

        return new EffectiveDocumentAccessModel(
            targetType.ToWire(),
            targetId,
            principalTypeWire,
            principalIdWire,
            allowed.OrderBy(a => a, StringComparer.Ordinal).ToList(),
            explained.OrderBy(e => e.Action, StringComparer.Ordinal).ToList(),
            _options.Mode.ToString());
    }

    private static bool MatchesPrincipal(DocumentAccessPolicyEntry policy, IReadOnlyList<PrincipalIdentity> identities) =>
        identities.Any(i => i.Type == policy.PrincipalType && string.Equals(i.Id, policy.PrincipalId.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlySet<string> TokensFor(DocumentAccessPrincipalType type, string id)
    {
        var token = type switch
        {
            DocumentAccessPrincipalType.User => $"user:{id.Trim()}",
            DocumentAccessPrincipalType.Role => $"role:{id.Trim()}",
            DocumentAccessPrincipalType.Company => $"company:{id.Trim()}",
            _ => null
        };
        return token is null ? new HashSet<string>() : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { token };
    }

    private static string Key(DocumentAccessTargetType type, string id) => $"{type}:{id.Trim()}".ToLowerInvariant();
}

public enum DocumentAccessDecision
{
    NoDecision = 0,
    Allow = 1,
    Deny = 2
}
