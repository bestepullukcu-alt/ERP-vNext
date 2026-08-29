using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules;

/// <summary>Conflict markers returned per matched row.</summary>
public static class TerritoryPreviewConflictStatus
{
    /// <summary>Exactly one target node claims this account.</summary>
    public const string None = "none";

    /// <summary>Several rules claim this account for DIFFERENT nodes; this row is the winning one.</summary>
    public const string ConflictWinner = "conflict-winner";

    /// <summary>Several rules claim this account for different nodes; this row lost on priority.</summary>
    public const string ConflictLoser = "conflict-loser";
}

/// <summary>
/// The FU03 matcher: pure, deterministic and completely side-effect free — it takes rules + account snapshots and
/// returns candidates and conflicts. It has no repository, no clock beyond the caller's <c>now</c>, and no way to
/// persist anything, which is what makes "preview never assigns" a structural property rather than a promise.
/// </summary>
public static class TerritoryAssignmentPreviewEngine
{
    public sealed record RuleMatch(TerritoryAssignmentRule Rule, string MatchReason);

    public sealed record AccountResult(
        TerritoryAccountSnapshot Account,
        IReadOnlyList<RuleMatch> Matches,
        RuleMatch Winner,
        bool HasConflict);

    public sealed record Outcome(
        IReadOnlyList<AccountResult> Results,
        IReadOnlyDictionary<Guid, int> MatchCountByRule,
        int UnmatchedAccounts);

    /// <summary>Rules whose effective window does not contain <paramref name="now"/> are reported as skipped by the
    /// caller; this method evaluates exactly the rules it is given.</summary>
    public static Outcome Evaluate(
        IReadOnlyList<TerritoryAssignmentRule> rules,
        IReadOnlyList<TerritoryAccountSnapshot> accounts)
    {
        var results = new List<AccountResult>();
        var matchCount = rules.ToDictionary(r => r.Id, _ => 0);
        var unmatched = 0;

        foreach (var account in accounts)
        {
            var matches = new List<RuleMatch>();
            foreach (var rule in rules)
            {
                // An explicit exclude always wins over any match on the same rule.
                if (rule.Criteria.ExcludeAccountIds.Contains(account.AccountId))
                {
                    continue;
                }

                if (TryMatch(rule, account, out var reason))
                {
                    matches.Add(new RuleMatch(rule, reason));
                    matchCount[rule.Id] = matchCount[rule.Id] + 1;
                }
            }

            if (matches.Count == 0)
            {
                unmatched++;
                continue;
            }

            var ordered = matches.OrderBy(m => m.Rule.Priority)
                .ThenBy(m => m.Rule.CreatedAt)
                .ThenBy(m => m.Rule.RuleCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // A conflict is "the same account is claimed for DIFFERENT nodes". Two rules pointing at the same node
            // are just redundant coverage, not a conflict.
            var distinctNodes = ordered.Select(m => m.Rule.TerritoryId).Distinct().Count();
            results.Add(new AccountResult(account, ordered, ordered[0], distinctNodes > 1));
        }

        return new Outcome(results, matchCount, unmatched);
    }

    private static bool TryMatch(TerritoryAssignmentRule rule, TerritoryAccountSnapshot account, out string reason)
    {
        reason = string.Empty;
        var c = rule.Criteria;
        var parts = new List<string>();

        // account-list: an explicit include list is a match on its own, independent of the attribute filters.
        if (c.IncludeAccountIds.Contains(account.AccountId))
        {
            reason = "explicit include list";
            return true;
        }

        if (string.Equals(rule.RuleType, TerritoryRuleTypes.AccountList, StringComparison.OrdinalIgnoreCase))
        {
            // An account-list rule matches ONLY through its include list.
            return false;
        }

        if (!MatchField(c.CountryRefs, account.CountryRef, "country", parts)) { return false; }
        if (!MatchField(c.CityRefs, account.CityRef, "city", parts)) { return false; }
        if (!MatchField(c.DistrictRefs, account.DistrictRef, "district", parts)) { return false; }
        if (!MatchField(c.AccountTypes, account.AccountType, "accountType", parts)) { return false; }
        if (!MatchField(c.AccountCategories, account.AccountCategory, "accountCategory", parts)) { return false; }
        if (!MatchField(c.AccountStatuses, account.Status, "accountStatus", parts)) { return false; }

        if (parts.Count == 0)
        {
            // No constraint was actually applied — validation forbids empty criteria, so this only happens for a rule
            // whose sole content is an exclude list. Such a rule must not claim the whole tenant.
            return false;
        }

        reason = string.Join(" AND ", parts);
        return true;
    }

    /// <summary>A constrained field must match one of the listed values (case-insensitive); an unconstrained field is
    /// ignored. A constrained field on an account that has no value is a miss, never a wildcard.</summary>
    private static bool MatchField(List<string> allowed, string? actual, string label, List<string> parts)
    {
        if (allowed.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actual)
            || !allowed.Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        parts.Add($"{label}={actual}");
        return true;
    }

    /// <summary>Preview-only advice derived from the winning rule's conflict policy. FU03 never resolves anything —
    /// enforcement belongs to FU05 apply / FU06 activation.</summary>
    public static string ResolutionSuggestion(string conflictPolicy, string winningRuleCode)
        => conflictPolicy?.Trim().ToLowerInvariant() switch
        {
            "block" => "Policy 'block': this conflict would prevent an apply; narrow the rules before FU05.",
            "priority" => $"Policy 'priority': rule '{winningRuleCode}' wins on priority.",
            "warn" => $"Policy 'warn': rule '{winningRuleCode}' wins on priority; the conflict is reported only.",
            "manual-review" => "Policy 'manual-review': a human decision is required before any apply.",
            _ => $"Winning rule on priority: '{winningRuleCode}'."
        };
}
