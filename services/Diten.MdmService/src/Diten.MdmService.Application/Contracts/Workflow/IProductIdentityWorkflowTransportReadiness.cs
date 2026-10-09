namespace Diten.MdmService.Application.Contracts.Workflow;

/// <summary>
/// Answers one question from this service's OWN configuration, without any network call: could the workflow
/// transport even attempt a call? Two things are checked — the Platform address and the service-identity provider
/// settings. Everything a remote side can say (Auth 401, Platform 401/409, a malformed response) is not this.
/// </summary>
public interface IProductIdentityWorkflowTransportReadiness
{
    /// <returns><c>null</c> when both are usable; otherwise the error code of the first one that is not.</returns>
    string? FindConfigurationFault();
}
