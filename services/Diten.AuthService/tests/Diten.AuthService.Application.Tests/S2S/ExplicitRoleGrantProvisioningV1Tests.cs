using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class ExplicitRoleGrantProvisioningV1Tests
{
    [Fact]
    public void Canonical_hash_is_deterministic_and_binds_role_permission_and_mutation()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var role = Guid.NewGuid(); var permission = Guid.NewGuid();
        var first = ExplicitRoleGrantProvisioningV1.Create(tenant, actor, role, permission, ExplicitRoleGrantMutationV1.Grant, "Key-A", "trusted-test");
        var second = ExplicitRoleGrantProvisioningV1.Create(tenant, actor, role, permission, ExplicitRoleGrantMutationV1.Grant, "Key-A", "trusted-test");
        Assert.Equal(first.CanonicalPayloadHash, second.CanonicalPayloadHash);
        Assert.NotEqual(first.CanonicalPayloadHash, ExplicitRoleGrantProvisioningV1.Create(tenant, actor, Guid.NewGuid(), permission, ExplicitRoleGrantMutationV1.Grant, "Key-A", "trusted-test").CanonicalPayloadHash);
        Assert.NotEqual(first.CanonicalPayloadHash, ExplicitRoleGrantProvisioningV1.Create(tenant, actor, role, Guid.NewGuid(), ExplicitRoleGrantMutationV1.Grant, "Key-A", "trusted-test").CanonicalPayloadHash);
        Assert.NotEqual(first.CanonicalPayloadHash, ExplicitRoleGrantProvisioningV1.Create(tenant, actor, role, permission, ExplicitRoleGrantMutationV1.Revoke, "Key-A", "trusted-test").CanonicalPayloadHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" key")]
    [InlineData("key ")]
    public void Idempotency_key_is_exact_and_not_normalized(string key) => Assert.Throws<S2SContractException>(() =>
        ExplicitRoleGrantProvisioningV1.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ExplicitRoleGrantMutationV1.Grant, key, "trusted"));

    [Fact]
    public void Empty_identities_and_unsupported_mutation_are_rejected()
    {
        Assert.Throws<S2SContractException>(() => ExplicitRoleGrantProvisioningV1.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ExplicitRoleGrantMutationV1.Grant, "key", "trusted"));
        Assert.Throws<S2SContractException>(() => ExplicitRoleGrantProvisioningV1.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), ExplicitRoleGrantMutationV1.Grant, "key", "trusted"));
        Assert.Throws<S2SContractException>(() => ExplicitRoleGrantProvisioningV1.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), ExplicitRoleGrantMutationV1.Grant, "key", "trusted"));
        Assert.Throws<S2SContractException>(() => ExplicitRoleGrantProvisioningV1.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, ExplicitRoleGrantMutationV1.Grant, "key", "trusted"));
        Assert.Throws<S2SContractException>(() => ExplicitRoleGrantProvisioningV1.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (ExplicitRoleGrantMutationV1)99, "key", "trusted"));
    }
}
