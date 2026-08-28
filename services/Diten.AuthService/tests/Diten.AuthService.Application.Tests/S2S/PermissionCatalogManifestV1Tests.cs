using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class PermissionCatalogManifestV1Tests
{
    [Fact]
    public void Canonical_gate_i_manifests_have_exact_pack_cardinality_and_bindings()
    {
        var manifests = GateIPermissionCatalogManifests.All;
        Assert.Equal(new[] { "MOD-0007", "MOD-0136", "MOD-0138", "MOD-0072", "MOD-0355" }, manifests.Select(x => x.OwnerModuleId));
        Assert.Equal(new[] { 8, 15, 16, 9, 25 }, manifests.Select(x => x.Entries.Count));
        Assert.Equal(73, manifests.Sum(x => x.Entries.Count));
        Assert.Equal(70, manifests.SelectMany(x => x.Entries).Select(x => x.PermissionKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(manifests, x =>
        {
            Assert.Equal(x.OwnerModuleId, x.ModuleEntitlementCode);
            Assert.Equal("diten.s2s.delegated.invoke", x.ProtocolScope);
            Assert.Equal(x.CanonicalPayloadHash, PermissionCatalogManifestV1.ComputeHash(x));
            PermissionCatalogManifestValidator.ValidateCanonical(x);
        });
        Assert.DoesNotContain(manifests.SelectMany(x => x.Entries), x => x.OperationId == "fpa.scenario-planning.comparators.execute");
    }

    [Fact]
    public void Management_governance_workload_reuse_keeps_mod0355_owner_entitlement_and_permissions_separate()
    {
        var decisions = GateIPermissionCatalogManifests.Mod0007;
        var modeling = GateIPermissionCatalogManifests.Mod0355;
        Assert.Equal(decisions.ClientId, modeling.ClientId);
        Assert.Equal(decisions.Audience, modeling.Audience);
        Assert.Equal(decisions.ServiceIdentity, modeling.ServiceIdentity);
        Assert.NotEqual(decisions.OwnerModuleId, modeling.OwnerModuleId);
        Assert.NotEqual(decisions.ModuleEntitlementCode, modeling.ModuleEntitlementCode);
        Assert.Equal("MOD-0355 checkpoint fa39a659ae58568bfa0a76dcb70a27980e19e0ca", modeling.RegistrationProvenance);
        Assert.Equal(25, modeling.Entries.Count);
        Assert.All(modeling.Entries, x => Assert.Equal(x.OperationId, x.PermissionKey));
        Assert.Empty(decisions.Entries.Select(x => x.PermissionKey).Intersect(modeling.Entries.Select(x => x.PermissionKey), StringComparer.Ordinal));
    }

    [Fact]
    public void Mod0355_manifest_has_exact_twenty_five_ordinal_pairs_without_aliases()
    {
        var expected = new[]
        {
            "management-governance.process-modeling.architectures.read",
            "management-governance.process-modeling.architectures.create",
            "management-governance.process-modeling.architectures.update",
            "management-governance.process-modeling.architectures.archive",
            "management-governance.process-modeling.definitions.read",
            "management-governance.process-modeling.definitions.create",
            "management-governance.process-modeling.definitions.update",
            "management-governance.process-modeling.definitions.archive",
            "management-governance.process-modeling.models.read",
            "management-governance.process-modeling.models.create",
            "management-governance.process-modeling.models.update",
            "management-governance.process-modeling.models.request-review",
            "management-governance.process-modeling.models.return-to-draft",
            "management-governance.process-modeling.models.publish",
            "management-governance.process-modeling.models.retire",
            "management-governance.process-modeling.models.create-revision",
            "management-governance.process-modeling.kpi-bindings.read",
            "management-governance.process-modeling.kpi-bindings.create",
            "management-governance.process-modeling.kpi-bindings.archive",
            "management-governance.process-modeling.improvements.read",
            "management-governance.process-modeling.improvements.create",
            "management-governance.process-modeling.improvements.update",
            "management-governance.process-modeling.improvements.qualify",
            "management-governance.process-modeling.improvements.close",
            "management-governance.process-modeling.improvements.record-outcome"
        };
        Assert.Equal(expected, GateIPermissionCatalogManifests.Mod0355.Entries.Select(x => x.OperationId));
        Assert.Equal(expected, GateIPermissionCatalogManifests.Mod0355.Entries.Select(x => x.PermissionKey));
    }

    [Fact]
    public void Shared_fpa_identity_does_not_merge_owner_or_entitlement()
    {
        var budgeting = GateIPermissionCatalogManifests.Mod0136;
        var scenario = GateIPermissionCatalogManifests.Mod0138;
        Assert.Equal(budgeting.ClientId, scenario.ClientId);
        Assert.Equal(budgeting.Audience, scenario.Audience);
        Assert.NotEqual(budgeting.OwnerModuleId, scenario.OwnerModuleId);
        Assert.Empty(budgeting.Entries.Select(x => x.PermissionKey).Intersect(scenario.Entries.Select(x => x.PermissionKey), StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(" Budgeting.budgets.read")]
    [InlineData("budgeting.budgets.read ")]
    [InlineData("Budgeting.budgets.read")]
    [InlineData("budgeting..budgets.read")]
    [InlineData("budgeting.*.read")]
    public void Exact_identifier_policy_rejects_normalization_alias_and_wildcard(string invalid)
    {
        var source = GateIPermissionCatalogManifests.Mod0136;
        var changed = source with { Entries = new[] { source.Entries[0] with { OperationId = invalid } }.Concat(source.Entries.Skip(1)).ToArray() };
        Assert.Throws<S2SContractException>(() => PermissionCatalogManifestValidator.ValidateCanonical(changed));
    }

    [Fact]
    public void Duplicate_operation_is_rejected_before_persistence()
    {
        var source = GateIPermissionCatalogManifests.Mod0007;
        var changed = source with { Entries = source.Entries.Append(source.Entries[0]).ToArray() };
        Assert.Throws<S2SContractException>(() => PermissionCatalogManifestValidator.ValidateShape(changed));
    }

    [Fact]
    public void Changed_payload_or_hash_is_rejected()
    {
        var source = GateIPermissionCatalogManifests.Mod0072;
        var changedPayload = source with { ClientId = source.ClientId + ".alias" };
        Assert.Throws<S2SContractException>(() => PermissionCatalogManifestValidator.ValidateCanonical(changedPayload));
        var changedHash = source with { CanonicalPayloadHash = new string('0', 64) };
        Assert.Throws<S2SContractException>(() => PermissionCatalogManifestValidator.ValidateCanonical(changedHash));
    }

    [Fact]
    public void Payload_equivalence_uses_ordinal_operation_and_permission_values()
    {
        var source = GateIPermissionCatalogManifests.Mod0007;
        var changed = source with { Entries = new[] { source.Entries[0] with { OperationId = source.Entries[0].OperationId.ToUpperInvariant() } }.Concat(source.Entries.Skip(1)).ToArray() };
        Assert.False(PermissionCatalogManifestValidator.SamePayload(source, changed));
    }

    [Fact]
    public void Shape_validation_does_not_trim_identifiers()
    {
        var source = GateIPermissionCatalogManifests.Mod0007;
        var changed = source with { Entries = new[] { source.Entries[0] with { OperationId = " " + source.Entries[0].OperationId } }.Concat(source.Entries.Skip(1)).ToArray() };
        Assert.Throws<S2SContractException>(() => PermissionCatalogManifestValidator.ValidateShape(changed));
    }

    [Fact]
    public void Shape_validation_rejects_wildcard_identifiers()
    {
        var source = GateIPermissionCatalogManifests.Mod0007;
        var changed = source with { Entries = new[] { source.Entries[0] with { OperationId = "decision-registry.*.read" } }.Concat(source.Entries.Skip(1)).ToArray() };
        Assert.Throws<S2SContractException>(() => PermissionCatalogManifestValidator.ValidateShape(changed));
    }
}
