namespace Diten.SupplyChainService.Api.Features.Claims;
public static class ClaimContractError
{
    public static object Create(string code, int status, Guid correlation) => new
    {
        error = new { code, message = Message(code), correlationId = correlation }, contractVersion = "v1"
    };
    public static string Message(string code) => code switch
    {
        "INVALID_REQUEST" => "Request schema or context is invalid.",
        "UNAUTHENTICATED" => "Authentication is required.",
        "FORBIDDEN" => "The requested operation is not permitted.",
        "CLAIM_NOT_FOUND" => "Claim or referenced resource was not found.",
        "UNSUPPORTED_MEDIA_TYPE" => "Content type is not supported.",
        "CLAIM_SHIPMENT_INELIGIBLE" => "Shipment is not eligible for a claim.",
        "CLAIM_CARRIER_MISMATCH" => "Carrier does not match the shipment.",
        "CLAIM_AMOUNT_INVALID" => "Claimed amount must be greater than zero.",
        "CLAIM_APPROVAL_AMOUNT_INVALID" => "Approved amount must be between zero and the claimed amount.",
        "CLAIM_APPROVED_AMOUNT_NOT_ALLOWED" => "Approved amount is not allowed for this action.",
        "INVALID_CLAIM_TRANSITION" => "Claim transition is not allowed.",
        "CLAIM_CORRELATION_MISMATCH" => "Correlation does not match the lifecycle root.",
        "IDEMPOTENCY_KEY_REUSED" => "Idempotency key was used for a different request.",
        "CLAIM_REFERENCE_INVALID" => "Reference response is invalid.",
        "CLAIM_REFERENCE_INCOMPLETE" => "Required reference data is unavailable.",
        "CLAIM_REFERENCE_UNAVAILABLE" => "Reference service is unavailable.",
        "CLAIM_STORAGE_UNAVAILABLE" => "Claim storage is unavailable.",
        _ => "An internal error occurred."
    };
}
