using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ItemMaster;

// MOD-0290-FU04 S1: the item screen API shapes (/api/mdm/items). The contract reads (product-master-bundle v1.1) are S4.
public static class ItemModels
{
    public sealed class ItemUomConversionInput
    {
        public string? FromUom { get; init; }
        public string? ToUom { get; init; }
        public string? Numerator { get; init; }
        public string? Denominator { get; init; }
    }

    // The fields a person writes (form_field_count = 11). The code, status, version and audit fields are the server's:
    // sent anyway, they are refused (ITEM_FIELD_FORBIDDEN) — not only on the screen.
    public class ItemFieldsInput
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? MaterialType { get; init; }
        public string? BaseUomCode { get; init; }
        public List<ItemUomConversionInput>? UomConversions { get; init; }
        public List<string>? StorageConditionCodes { get; init; }
        public bool? LotControlled { get; init; }
        public bool? Stockable { get; init; }
        public int? ShelfLifeDays { get; init; }
        public int? RetestDays { get; init; }
        public int? MinRemainingShelfLifeDays { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class CreateItemRequest : ItemFieldsInput
    {
        public string? IdempotencyKey { get; init; }
    }

    public sealed class UpdateItemRequest : ItemFieldsInput
    {
        public int? ExpectedVersion { get; init; }
    }

    public sealed class ItemLifecycleRequest
    {
        public int? ExpectedVersion { get; init; }
        public string? Reason { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record ItemUomConversionDto(string FromUom, string ToUom, string Numerator, string Denominator);

    public sealed record ItemDto(
        Guid Id,
        string CanonicalCode,
        string Name,
        string? Description,
        string MaterialType,
        string BaseUomCode,
        IReadOnlyList<ItemUomConversionDto> UomConversions,
        IReadOnlyList<string> StorageConditionCodes,
        bool LotControlled,
        bool Stockable,
        int? ShelfLifeDays,
        int? RetestDays,
        int? MinRemainingShelfLifeDays,
        ItemLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt)
    {
        // MOD-0290-FU04 S5: the reader's actions on this item (ItemActions); null where the answer is not a read.
        public IReadOnlyList<string>? AvailableActions { get; init; }
    }

    // MOD-0290-FU04 S5b (BL-600): the choices of the three item lists, in their order, with the attributes the forms use
    // (a unit's dimension and precision; a storage condition's kind — O-2: at most one TEMPERATURE). A list to choose
    // from, never the proof of a choice: the write resolves every chosen code exactly.
    public sealed record ItemOptionDto(
        string Code,
        string DisplayName,
        int SortOrder,
        IReadOnlyDictionary<string, string?> Attributes);

    public sealed record ItemOptionsDto(
        string CatalogVersion,
        IReadOnlyList<ItemOptionDto> MaterialTypes,
        IReadOnlyList<ItemOptionDto> Uoms,
        IReadOnlyList<ItemOptionDto> StorageConditions);

    public sealed record ItemListDto(IReadOnlyList<ItemDto> Items, long Total, int Page, int PageSize);
}
