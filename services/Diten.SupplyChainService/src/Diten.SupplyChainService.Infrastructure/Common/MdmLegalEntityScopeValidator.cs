using System.Net;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Infrastructure.Common;

/// <summary>
/// Asks MDM whether a legal entity is referenceable for the tenant in the caller's token, through the gateway, the
/// same seam ReturnReferenceReader and ClaimReferenceReader already use. GET api/legal-entities/{id}/lookup-validation
/// is MDM's own endpoint (LegalEntitiesController, ValidateLegalEntityReferenceHandler): it loads by id through a
/// repository that composes TenantFilter, and answers 404 when the entity is absent, foreign, or not Active.
///
/// The caller's Authorization is forwarded rather than a service identity, because SupplyChain holds no MDM service
/// identity — Platform's MdmServiceIdentity pair is Platform-to-MDM only. The consequence is a real permission
/// requirement, recorded as an owner item (Q470): a SupplyChain user needs mdm.legal-entities.read, which is seeded
/// in DataSeeder but is not in the SupplyChain default role template.
/// </summary>
public sealed class MdmLegalEntityScopeValidator(HttpClient client, IConfiguration configuration) : ILegalEntityScopeValidator
{
    public const string BaseUrlKey = "LegalEntity:ReferenceBaseUrl";

    public async Task<LegalEntityScopeOutcome> ValidateAsync(
        Guid tenantId,
        Guid legalEntityId,
        string authorization,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        // A missing or non-http base URL is a configuration fault, not an authorization answer. Refusing here keeps
        // the fail-closed promise: an unconfigured service cannot accept a legal entity it never checked.
        if (!Uri.TryCreate(configuration[BaseUrlKey], UriKind.Absolute, out var root)
            || root.Scheme is not ("http" or "https"))
        {
            return LegalEntityScopeOutcome.Unavailable;
        }

        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(authorization))
        {
            return LegalEntityScopeOutcome.NotReferenceable;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(root, "api/legal-entities/" + legalEntityId.ToString("D") + "/lookup-validation"));
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        request.Headers.Add("X-Correlation-Id", correlationId.ToString("D"));

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => LegalEntityScopeOutcome.Valid,
                HttpStatusCode.NotFound => LegalEntityScopeOutcome.NotReferenceable,
                // 403 means the caller lacks mdm.legal-entities.read. That is the caller's authorization problem and
                // not a statement about the legal entity, so it is refused — but as unavailable, because treating it
                // as NotReferenceable would report a permission gap as a missing record (Q420's distinction).
                HttpStatusCode.Forbidden => LegalEntityScopeOutcome.Unavailable,
                _ => LegalEntityScopeOutcome.Unavailable,
            };
        }
        catch (HttpRequestException) { return LegalEntityScopeOutcome.Unavailable; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return LegalEntityScopeOutcome.Unavailable; }
    }
}
