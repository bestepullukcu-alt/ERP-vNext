namespace Diten.SupplyChainService.Api.Features.Carriers;
public static class CarrierContractError
{
    public static string Message(string code, int status) => code switch
    {
        "CARRIER_NOT_FOUND" => "Carrier not found.",
        "CARRIER_CODE_CONFLICT" => "Carrier code is already reserved.",
        "IDEMPOTENCY_KEY_REUSED" => "Idempotency-Key was used with a different payload.",
        "INVALID_CARRIER_TRANSITION" => "Carrier lifecycle transition is invalid.",
        "PERSISTENCE_UNAVAILABLE" => "Persistence outcome is unavailable; retry the request using the same idempotency key when applicable.",
        // BL-652: DEPENDENCY_UNAVAILABLE was in neither switch, so a 503 fell to the final default and reached the
        // caller as "Request schema validation failed." — measured 2026-10-17, when a token holding all 25
        // supplychain.* keys but not mdm.legal-entities.read made legal-entity validation answer 503 and a VALID
        // payload was reported as a schema error. Both arms are covered: the code, because that is this file's
        // primary dispatch, and the status, because another code may yet arrive carrying 503.
        "DEPENDENCY_UNAVAILABLE" => "A required dependency is unavailable; the request was not applied.",
        _ => status switch { 401 => "Authentication required.", 403 => "Required authorization context or permission is missing.",
            415 => "Request content type is not supported.", 500 => "An unexpected internal error occurred.",
            503 => "A required dependency is unavailable; the request was not applied.",
            _ => "Request schema validation failed." }
    };
    public static object Create(string code, int status, Guid correlation, string? message = null) =>
        new { error = new { code, message = message ?? Message(code, status), correlationId = correlation }, contractVersion = "v1" };
}
