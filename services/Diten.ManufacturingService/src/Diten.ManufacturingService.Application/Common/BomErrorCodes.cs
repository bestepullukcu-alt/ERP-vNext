namespace Diten.ManufacturingService.Application.Common;

/// <summary>
/// BOM contract hata kodları. Handler <c>Response.Fail(code, status)</c> döner; Api katmanı bunu frozen
/// <c>{ error: { code, message, correlationId }, contractVersion }</c> gövdesine çevirir.
/// </summary>
public static class BomErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string UnknownBom = "UNKNOWN_BOM";
    public const string UnknownItem = "UNKNOWN_ITEM";
    public const string SelfReference = "SELF_REFERENCE";
    public const string BomCycle = "BOM_CYCLE";
    public const string BomNotDraft = "BOM_NOT_DRAFT";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string ChangeControlRejected = "CHANGE_CONTROL_REJECTED";
    public const string PersistenceUnavailable = "PERSISTENCE_UNAVAILABLE";
    public const string LegalEntityRequired = "LEGAL_ENTITY_REQUIRED";
    public const string LegalEntityNotReferenceable = "LEGAL_ENTITY_NOT_REFERENCEABLE";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";

    public static string Message(string code) => code switch
    {
        UnknownBom => "bom bulunamadı",
        UnknownItem => "One or more item ids are unknown to the product master.",
        SelfReference => "A BOM cannot list its own item as a component.",
        BomCycle => "Releasing this version would make the item a component of itself.",
        BomNotDraft => "Only a draft BOM version can be changed or deleted.",
        ConcurrencyConflict => "The BOM version was changed by someone else; reload and retry.",
        ChangeControlRejected => "The change control reference was not accepted.",
        PersistenceUnavailable => "The change could not be saved; nothing was written. Retry the request.",
        InternalError => "An unexpected internal error occurred.",
        LegalEntityRequired => "Choose a legal entity: X-Legal-Entity-Id is required.",
        LegalEntityNotReferenceable => "The legal entity does not belong to this tenant or is not active.",
        DependencyUnavailable => "The legal entity could not be checked (master data unavailable); nothing was read or written.",
        _ => "Request validation failed."
    };
}
