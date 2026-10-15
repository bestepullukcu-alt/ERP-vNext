using Diten.MdmService.Application.Contracts.Workflow;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.Workflow;

/// <summary>
/// Asks the transport's own validators — the same two checks <see cref="PlatformProductIdentityWorkflowClient"/> and
/// <see cref="AuthProductIdentityWorkflowServiceIdentityProvider"/> run when a call is made — so "not configured" is
/// decided by one rule, not by a second copy of it.
/// </summary>
public sealed class ProductIdentityWorkflowTransportReadiness(
    IOptions<ProductIdentityWorkflowClientOptions> client,
    IOptions<AuthProductIdentityWorkflowServiceIdentityProviderOptions> identity)
    : IProductIdentityWorkflowTransportReadiness
{
    public const string ClientConfigurationInvalid = "PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID";

    public string? FindConfigurationFault()
    {
        if (!PlatformProductIdentityWorkflowClient.IsValidConfiguration(client.Value))
        {
            return ClientConfigurationInvalid;
        }

        try
        {
            AuthProductIdentityWorkflowServiceIdentityProvider.EnsureValidConfiguration(identity.Value);
            return null;
        }
        catch (ProductIdentityWorkflowServiceIdentityException exception)
        {
            return exception.ErrorCode;
        }
    }
}
