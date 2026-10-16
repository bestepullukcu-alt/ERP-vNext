namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 S5: the store could not run the write (no transaction on a standalone mongod, a primary
/// that stepped down, a store that is away). Said with this code and a 503 — never an uncoded 500, and never mistaken
/// for "the draft changed". The same code the bounded claim answers with (ProductIdentityWorkflowClaims).
/// </summary>
public static class ProductIdentityStore
{
    public const string Unavailable = "PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE";
}
