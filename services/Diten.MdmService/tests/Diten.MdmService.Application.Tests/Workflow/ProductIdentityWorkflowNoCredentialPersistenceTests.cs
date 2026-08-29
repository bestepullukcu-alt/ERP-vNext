using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Xunit;

namespace Diten.MdmService.Application.Tests.Workflow;

public sealed class ProductIdentityWorkflowNoCredentialPersistenceTests
{
    [Fact]
    public void Workflow_transport_and_domain_models_expose_no_credential_or_authorization_storage()
    {
        var types = new[]
        {
            typeof(ProductIdentityWorkflowStartRequest),
            typeof(ProductIdentityWorkflowStartResultRequest),
            typeof(ProductIdentityWorkflowTerminalEvidenceRequest),
            typeof(ProductIdentityWorkflowStartResult),
            typeof(ProductIdentityWorkflowTerminalEvidence),
            typeof(GlobalProduct),
            typeof(GlobalProductIdentityWorkflowOperation),
            typeof(FirstGskuIdentityWorkflowOperation),
            typeof(Domain.ValueObjects.FirstGskuIdentityWorkflowBinding)
        };
        var forbidden = new[] { "AccessToken", "ServiceToken", "DelegatedToken", "Authorization", "ClientSecret", "Credential" };

        foreach (var property in types.SelectMany(type => type.GetProperties().Select(property => (type, property))))
            Assert.DoesNotContain(forbidden, word => property.property.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Service_identity_is_an_ephemeral_application_contract_not_a_domain_entity()
    {
        Assert.False(typeof(EntityBase).IsAssignableFrom(typeof(ProductIdentityWorkflowServiceIdentity)));
        Assert.Equal(["AccessToken", "ExpiresAtUtc"], typeof(ProductIdentityWorkflowServiceIdentity)
            .GetProperties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
