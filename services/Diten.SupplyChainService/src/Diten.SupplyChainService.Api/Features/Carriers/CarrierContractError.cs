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
        _ => status switch { 401 => "Authentication required.", 403 => "Required authorization context or permission is missing.",
            415 => "Request content type is not supported.", 500 => "An unexpected internal error occurred.", _ => "Request schema validation failed." }
    };
    public static object Create(string code, int status, Guid correlation, string? message = null) =>
        new { error = new { code, message = message ?? Message(code, status), correlationId = correlation }, contractVersion = "v1" };
}
