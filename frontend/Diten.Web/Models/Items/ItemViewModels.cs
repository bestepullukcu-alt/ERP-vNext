using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Items;

/// <summary>
/// MOD-0290-FU04 S5 — the Items form (Create / Edit). The page is drawn empty and index.js fills it from the proxy's
/// <c>api/{id}</c>; the form is sent as JSON (<see cref="ItemWriteInput"/>), never posted as an MVC form. The bound names
/// here are the eleven fields MDM accepts (§4.1, form_field_count = 11), so the labels and the required markers agree.
/// </summary>
public sealed class ItemFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public string MaterialType { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string BaseUomCode { get; set; } = string.Empty;

    // Several storage-condition codes in one text field (no screen list of the values exists yet — see ItemsController).
    public string? StorageConditionCodes { get; set; }

    // Drawn as a switch (asp-for needs bool); the value itself is MDM's bool? (S2 R-04: null = never filled) — index.js
    // draws a null indeterminate and sends it as null, so it is never saved as a silent "no".
    public bool LotControlled { get; set; }

    public bool Stockable { get; set; }

    public int? ShelfLifeDays { get; set; }

    public int? RetestDays { get; set; }

    public int? MinRemainingShelfLifeDays { get; set; }
}

public sealed class ItemUomConversionInput
{
    public string? FromUom { get; set; }
    public string? ToUom { get; set; }
    public string? Numerator { get; set; }
    public string? Denominator { get; set; }
}

/// <summary>
/// What the browser sends to create or update an item. Only these fields are read; the proxy forwards exactly MDM's
/// eleven (+ the idempotency key on create, the expected version on update) — anything else the browser adds is dropped.
/// </summary>
public sealed class ItemWriteInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? MaterialType { get; set; }
    public string? BaseUomCode { get; set; }
    public List<ItemUomConversionInput>? UomConversions { get; set; }
    public List<string>? StorageConditionCodes { get; set; }
    public bool? LotControlled { get; set; }
    public bool? Stockable { get; set; }
    public int? ShelfLifeDays { get; set; }
    public int? RetestDays { get; set; }
    public int? MinRemainingShelfLifeDays { get; set; }
    public int? ExpectedVersion { get; set; }
    public string? FormAttemptToken { get; set; }
}
