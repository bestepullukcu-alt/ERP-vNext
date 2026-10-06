namespace Diten.ManufacturingService.Infrastructure.Authorization;

/// <summary>
/// MOD-0193 izin anahtarları (PKS-001: küçük harf, noktalı, ≥ 3 parça). Her biri bir uçta uygulanır VE modül
/// manifestinde bildirilir (sayfa <c>RequiredPermission</c> ya da eylem anahtarı) — bildirilmeyen anahtar Auth'a
/// ulaşmaz ve ucu herkese 403 olur.
/// </summary>
public static class BomPermissions
{
    public const string Read = "manufacturing.bom.read";
    public const string Create = "manufacturing.bom.create";
    public const string Update = "manufacturing.bom.update";
    public const string Release = "manufacturing.bom.release";
    public const string Delete = "manufacturing.bom.delete";

    public static readonly string[] All = [Read, Create, Update, Release, Delete];
}
