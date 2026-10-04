namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — the product's ONE name in an e-mail: the sender name of a message with no tenant behind it, and the
/// "via" part of a tenant's sender name. AuthService used to say "Diten ERP" and Platform "Diten PPM" from two
/// <c>Smtp:FromName</c> settings; a recipient saw two products. The name is a constant rather than a setting so
/// that it cannot drift between services again — changing it is this one line.
/// </summary>
public static class EmailProduct
{
    public const string Name = "Di10";
}
