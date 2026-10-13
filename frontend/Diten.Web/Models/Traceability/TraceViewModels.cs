namespace Diten.Web.Models.Traceability;

/// <summary>Yeni Lot / Düzenle page model: the mode, the lot being edited, and the form's idempotency key (minted on GET).</summary>
public sealed class LotFormPageViewModel
{
    public bool IsEdit { get; init; }
    public string? LotId { get; init; }
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString("D");

    public static LotFormPageViewModel ForCreate() => new();

    public static LotFormPageViewModel ForEdit(string? lotId) =>
        new() { IsEdit = true, LotId = Guid.TryParse(lotId, out var id) ? id.ToString("D") : null };
}

/// <summary>A page that is opened for one lot (Lot Detayı, Hareket Geçmişi, İzlenebilirlik), from the address.</summary>
public sealed class LotPageViewModel
{
    public string? LotId { get; init; }
    public string? Serial { get; init; }

    public static LotPageViewModel From(string? lotId, string? serial = null) => new()
    {
        LotId = Guid.TryParse(lotId, out var id) ? id.ToString("D") : null,
        Serial = string.IsNullOrWhiteSpace(serial) ? null : LotFieldRules.NormalizeSerial(serial)
    };
}
