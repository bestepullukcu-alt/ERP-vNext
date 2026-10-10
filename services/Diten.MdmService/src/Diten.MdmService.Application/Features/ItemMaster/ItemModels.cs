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
        // FU04 S2 (R-04): null = not filled (a legacy draft); an Active item never shows null.
        bool? LotControlled,
        bool Stockable,
        int? ShelfLifeDays,
        int? RetestDays,
        int? MinRemainingShelfLifeDays,
        ItemLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record ItemListDto(IReadOnlyList<ItemDto> Items, long Total, int Page, int PageSize);
}
