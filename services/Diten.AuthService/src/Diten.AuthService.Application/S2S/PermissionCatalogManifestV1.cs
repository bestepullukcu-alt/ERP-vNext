using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.S2S;

public sealed record PermissionCatalogOperationV1(string OperationId, string PermissionKey);

/// <summary>Application-internal Gate-I registration model. It is not an HTTP or producer contract.</summary>
public sealed record PermissionCatalogManifestV1(
    string OwnerModuleId,
    string ModuleEntitlementCode,
    string ServiceIdentity,
    string ClientId,
    string Audience,
    string ProtocolScope,
    string ManifestVersion,
    IReadOnlyList<PermissionCatalogOperationV1> Entries,
    string CanonicalPayloadHash,
    string RegistrationProvenance,
    DateTimeOffset RegisteredAtUtc)
{
    public const string ContractName = nameof(PermissionCatalogManifestV1);
    public const int ContractVersion = 1;

    public static PermissionCatalogManifestV1 Create(
        string ownerModuleId, string moduleEntitlementCode, string serviceIdentity,
        string clientId, string audience, string manifestVersion,
        IEnumerable<PermissionCatalogOperationV1> entries, string provenance,
        DateTimeOffset registeredAtUtc)
    {
        var materialized = entries.ToArray();
        var value = new PermissionCatalogManifestV1(ownerModuleId, moduleEntitlementCode, serviceIdentity,
            clientId, audience, "diten.s2s.delegated.invoke", manifestVersion, materialized,
            string.Empty, provenance, registeredAtUtc);
        PermissionCatalogManifestValidator.ValidateShape(value);
        return value with { CanonicalPayloadHash = ComputeHash(value) };
    }

    public static string ComputeHash(PermissionCatalogManifestV1 value)
    {
        var fields = new List<string>
        {
            ContractName, ContractVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.OwnerModuleId, value.ModuleEntitlementCode, value.ServiceIdentity, value.ClientId,
            value.Audience, value.ProtocolScope, value.ManifestVersion, value.RegistrationProvenance
        };
        foreach (var entry in value.Entries) { fields.Add(entry.OperationId); fields.Add(entry.PermissionKey); }
        var canonical = string.Concat(fields.Select(x => $"{Encoding.UTF8.GetByteCount(x)}:{x}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public static class PermissionCatalogManifestValidator
{
    public static void ValidateShape(PermissionCatalogManifestV1 value)
    {
        S2SExactValue.Required(value.OwnerModuleId, nameof(value.OwnerModuleId));
        S2SExactValue.Required(value.ModuleEntitlementCode, nameof(value.ModuleEntitlementCode));
        S2SExactValue.Required(value.ServiceIdentity, nameof(value.ServiceIdentity));
        S2SExactValue.RequiredLowercase(value.ClientId, nameof(value.ClientId));
        S2SExactValue.RequiredLowercase(value.Audience, nameof(value.Audience));
        S2SExactValue.RequiredLowercase(value.ProtocolScope, nameof(value.ProtocolScope));
        S2SExactValue.RequiredLowercase(value.ManifestVersion, nameof(value.ManifestVersion));
        S2SExactValue.Required(value.RegistrationProvenance, nameof(value.RegistrationProvenance));
        if (!string.Equals(value.OwnerModuleId, value.ModuleEntitlementCode, StringComparison.Ordinal))
            throw new S2SContractException("Owner and entitlement identities must remain exact and independent.", nameof(value.ModuleEntitlementCode));
        if (value.Entries.Count == 0) throw new S2SContractException("Entries are required.", nameof(value.Entries));
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in value.Entries)
        {
            ValidateKey(entry.OperationId, nameof(entry.OperationId));
            ValidateKey(entry.PermissionKey, nameof(entry.PermissionKey));
            if (!operations.Add(entry.OperationId)) throw new S2SContractException("Duplicate exact operation.", nameof(value.Entries));
        }
    }

    public static void ValidateCanonical(PermissionCatalogManifestV1 value)
    {
        ValidateShape(value);
        var expected = GateIPermissionCatalogManifests.All.SingleOrDefault(x =>
            string.Equals(x.OwnerModuleId, value.OwnerModuleId, StringComparison.Ordinal));
        if (expected is null || !SamePayload(expected, value))
            throw new S2SContractException("Unknown or non-canonical Gate-I manifest.", nameof(value));
        if (!string.Equals(value.CanonicalPayloadHash, PermissionCatalogManifestV1.ComputeHash(value), StringComparison.Ordinal))
            throw new S2SContractException("Canonical payload hash mismatch.", nameof(value.CanonicalPayloadHash));
    }

    public static bool SamePayload(PermissionCatalogManifestV1 left, PermissionCatalogManifestV1 right) =>
        string.Equals(left.OwnerModuleId, right.OwnerModuleId, StringComparison.Ordinal) &&
        string.Equals(left.ModuleEntitlementCode, right.ModuleEntitlementCode, StringComparison.Ordinal) &&
        string.Equals(left.ServiceIdentity, right.ServiceIdentity, StringComparison.Ordinal) &&
        string.Equals(left.ClientId, right.ClientId, StringComparison.Ordinal) &&
        string.Equals(left.Audience, right.Audience, StringComparison.Ordinal) &&
        string.Equals(left.ProtocolScope, right.ProtocolScope, StringComparison.Ordinal) &&
        string.Equals(left.ManifestVersion, right.ManifestVersion, StringComparison.Ordinal) &&
        string.Equals(left.RegistrationProvenance, right.RegistrationProvenance, StringComparison.Ordinal) &&
        left.Entries.SequenceEqual(right.Entries);

    private static void ValidateKey(string value, string name)
    {
        S2SExactValue.RequiredLowercase(value, name);
        if (value.StartsWith(".", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal) ||
            value.Contains("..", StringComparison.Ordinal) || value.Split('.').Length < 3)
            throw new S2SContractException("Exact dotted identifier is malformed.", name);
    }
}

public static class GateIPermissionCatalogManifests
{
    private static PermissionCatalogOperationV1 E(string operation, string permission) => new(operation, permission);
    private static PermissionCatalogManifestV1 M(string owner, string service, string client, string audience, string sha,
        params PermissionCatalogOperationV1[] entries) => PermissionCatalogManifestV1.Create(owner, owner, service, client,
            audience, sha, entries, $"{owner} checkpoint {sha}", DateTimeOffset.UnixEpoch);

    public static readonly PermissionCatalogManifestV1 Mod0007 = M("MOD-0007", "Diten.ManagementGovernanceService", "diten.management-governance", "diten-management-governance-service", "7bdbd37e16c72cd80f081612a104cc3af7e2b4cd",
        E("decision-registry.decisions.read.v1", "management-governance.decisions.read"), E("decision-registry.drafts.create.v1", "management-governance.decisions.create"),
        E("decision-registry.drafts.revise.v1", "management-governance.decisions.revise"), E("decision-registry.drafts.soft-delete.v1", "management-governance.decisions.revise"),
        E("decision-registry.drafts.publish.v1", "management-governance.decisions.publish"), E("decision-registry.decisions.supersede.v1", "management-governance.decisions.supersede"),
        E("decision-registry.decisions.withdraw.v1", "management-governance.decisions.withdraw"), E("decision-registry.decision-references.validate.v1", "management-governance.decision-references.validate"));

    public static readonly PermissionCatalogManifestV1 Mod0136 = M("MOD-0136", "Diten.FpaService", "diten.fpa", "diten-fpa-service", "937aabf43683eac9a240f9101ee84c66db55423a",
        E("budgeting.budgets.read", "budgeting.budgets.read"), E("budgeting.budgets.create", "budgeting.budgets.create"), E("budgeting.budgets.update", "budgeting.budgets.update"), E("budgeting.budgets.archive", "budgeting.budgets.archive"),
        E("budgeting.budget-version-drafts.read", "budgeting.budget-version-drafts.read"), E("budgeting.budget-version-drafts.create", "budgeting.budget-version-drafts.create"), E("budgeting.budget-version-drafts.update", "budgeting.budget-version-drafts.update"), E("budgeting.budget-version-drafts.abandon", "budgeting.budget-version-drafts.abandon"),
        E("budgeting.budget-versions.read", "budgeting.budget-versions.read"), E("budgeting.budget-versions.certify", "budgeting.budget-versions.certify"), E("budgeting.budget-versions.retire", "budgeting.budget-versions.retire"),
        E("budgeting.funding-baseline-selections.read", "budgeting.funding-baseline-selections.read"), E("budgeting.funding-baseline-selections.replace", "budgeting.funding-baseline-selections.replace"), E("budgeting.funding-baseline-selections.close", "budgeting.funding-baseline-selections.close"), E("budgeting.budget-version-references.validate", "budgeting.budget-version-references.validate"));

    public static readonly PermissionCatalogManifestV1 Mod0138 = M("MOD-0138", "Diten.FpaService", "diten.fpa", "diten-fpa-service", "066d16c80b966a63aaa7430ee8dd14c120e7a4c2",
        E("fpa.scenario-planning.scenarios.read", "fpa.scenario-planning.scenarios.read"), E("fpa.scenario-planning.scenarios.create", "fpa.scenario-planning.scenarios.create"), E("fpa.scenario-planning.scenarios.update", "fpa.scenario-planning.scenarios.update"),
        E("fpa.scenario-planning.version-drafts.read", "fpa.scenario-planning.version-drafts.read"), E("fpa.scenario-planning.version-drafts.create", "fpa.scenario-planning.version-drafts.create"), E("fpa.scenario-planning.version-drafts.update", "fpa.scenario-planning.version-drafts.update"), E("fpa.scenario-planning.version-drafts.abandon", "fpa.scenario-planning.version-drafts.abandon"),
        E("fpa.scenario-planning.versions.read", "fpa.scenario-planning.versions.read"), E("fpa.scenario-planning.versions.publish", "fpa.scenario-planning.versions.publish"), E("fpa.scenario-planning.versions.retire", "fpa.scenario-planning.versions.retire"),
        E("fpa.scenario-planning.comparators.read", "fpa.scenario-planning.comparators.read"), E("fpa.scenario-planning.comparators.run", "fpa.scenario-planning.comparators.run"),
        E("fpa.scenario-planning.selections.read", "fpa.scenario-planning.selections.read"), E("fpa.scenario-planning.selections.replace", "fpa.scenario-planning.selections.replace"), E("fpa.scenario-planning.selections.close", "fpa.scenario-planning.selections.close"), E("fpa.scenario-planning.references.validate", "fpa.scenario-planning.references.validate"));

    public static readonly PermissionCatalogManifestV1 Mod0072 = M("MOD-0072", "Diten.DecisionIntelligenceService", "diten.decision-intelligence", "diten-decision-intelligence-service", "5e5088ef6a5298b09b1dfcece9cf10ad2375aa29",
        E("outcome-tracking.outcomes.read", "decision-intelligence.outcomes.read"), E("outcome-tracking.outcomes.create", "decision-intelligence.outcomes.create"), E("outcome-tracking.outcomes.publish-version", "decision-intelligence.outcomes.version"), E("outcome-tracking.outcomes.retire", "decision-intelligence.outcomes.version"),
        E("outcome-tracking.measurements.append", "decision-intelligence.measurements.append"), E("outcome-tracking.measurements.correct", "decision-intelligence.measurements.correct"), E("outcome-tracking.decision-links.create", "decision-intelligence.decision-links.manage"), E("outcome-tracking.decision-links.retire", "decision-intelligence.decision-links.manage"), E("outcome-tracking.outcome-references.validate", "decision-intelligence.outcome-references.validate"));

    public static IReadOnlyList<PermissionCatalogManifestV1> All { get; } = new[] { Mod0007, Mod0136, Mod0138, Mod0072 };
}
