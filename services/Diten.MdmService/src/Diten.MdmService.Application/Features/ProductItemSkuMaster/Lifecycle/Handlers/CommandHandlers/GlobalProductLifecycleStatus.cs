namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/// <summary>
/// The HTTP status a Global Product lifecycle write answers for its repository error code.
///
/// <para>Moved here, unchanged, from the removed <c>SubmitGlobalProductIdentityHandler</c> (WP-GP-AUDIT-TRAIL-01 D1:
/// the submit and the decision reconciliation are the identity workflow processor's work, and the two command
/// handlers nobody sent were deleted). The retirement handler still answers with it.</para>
/// </summary>
internal static class GlobalProductLifecycleStatus
{
    internal static int For(string? code) => code switch
    {
        "PRODUCT_IDENTITY_NOT_FOUND" => 404,
        "AUDIT_INTENT_CAPACITY_EXCEEDED" => 503,
        "WORKFLOW_BINDING_CONTRACT_INVALID" or "AUDIT_INTENT_CONTRACT_INVALID" => 400,
        _ => 409
    };
}
