using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster;

public static class ProductItemSkuMasterModels
{
    public sealed record CodeReservationDto(
        Guid ReservationId,
        string ReservedCode,
        CodeBearingEntityType EntityType,
        CodeReservationState State,
        CodeReservationBindingState BindingState,
        int Version);

    public sealed class CreateGlobalProductDraftRequest
    {
        public string? GlobalProductName { get; init; }
        public Guid ReservationId { get; init; }
        public int ExpectedReservationVersion { get; init; }
        public string IdempotencyKey { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class ReserveGlobalProductCodeRequest
    {
        public string? GlobalProductName { get; init; }
        public string IdempotencyKey { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record GlobalProductDraftDto(
        Guid GlobalProductId,
        string CanonicalCode,
        string GlobalProductName,
        Guid CodeReservationId,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        CodeReservationBindingState CodeBindingState,
        bool BindingReconciliationRequired);

    public sealed record GlobalProductListItemDto(
        Guid Id,
        string CanonicalCode,
        string GlobalProductName,
        ProductIdentityLifecycleStatus LifecycleStatus);

    public sealed record GlobalProductDetailDto(
        Guid Id,
        string CanonicalCode,
        string GlobalProductName,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record GlobalProductSelectorDto(
        Guid Id,
        string CanonicalCode,
        string GlobalProductName);

    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items,
        int PageNumber,
        int PageSize,
        long TotalCount);

    public sealed class CreateFirstGskuDraftRequest
    {
        public Guid GlobalProductId { get; init; }
        public Guid GskuReservationId { get; init; }
        public int ExpectedReservationVersion { get; init; }
        public string CreationCommandId { get; init; } = string.Empty;
        public decimal PackQuantity { get; init; }
        public string PackUomCode { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class UpdateGskuDraftRequest
    {
        public Guid GskuId { get; init; }
        public int ExpectedVersion { get; init; }
        public decimal PackQuantity { get; init; }
        public string PackUomCode { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record ReferenceCatalogSelectionDto(
        string SetCode,
        string ValueCode,
        Guid CatalogVersionId,
        int CatalogVersionNumber,
        ReferenceCatalogResolutionMode ResolutionMode,
        DateTimeOffset ResolvedAtUtc);

    public sealed record FirstGskuDraftDto(
        Guid ProductDefinitionRevisionId,
        string RevisionIdentifier,
        Guid GskuId,
        string CanonicalCode,
        Guid CodeReservationId,
        string CreationCommandId,
        decimal PackQuantity,
        string PackUomCode,
        ReferenceCatalogSelectionDto PackApplicabilitySelection,
        ReferenceCatalogSelectionDto PackUomSelection,
        int Version,
        CodeReservationBindingState CodeBindingState,
        bool ReconciliationRequired);

    public sealed class CreateFirstGskuDraftFacadeRequest
    {
        public Guid GlobalProductId { get; init; }
        public decimal PackQuantity { get; init; }
        public string PackUomCode { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record GskuDraftResponse(
        Guid GskuId,
        string CanonicalCode,
        Guid GlobalProductId,
        Guid ProductDefinitionRevisionId,
        string RevisionIdentifier,
        decimal PackQuantity,
        string PackUomCode,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version);

    public sealed record GskuListItemDto(
        Guid Id,
        string CanonicalCode,
        Guid GlobalProductId,
        string GlobalProductCanonicalCode,
        string GlobalProductName,
        Guid ProductDefinitionRevisionId,
        string RevisionIdentifier,
        decimal PackQuantity,
        string PackUomCode,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record GskuDetailDto(
        Guid Id,
        string CanonicalCode,
        Guid GlobalProductId,
        string GlobalProductCanonicalCode,
        string GlobalProductName,
        Guid ProductDefinitionRevisionId,
        string RevisionIdentifier,
        decimal PackQuantity,
        string PackUomCode,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record GskuCreateGlobalProductOptionDto(
        Guid Id,
        string CanonicalCode,
        string GlobalProductName);

    public sealed record GskuCreateUomOptionDto(
        string Code,
        string DisplayText,
        int SortOrder,
        int MaximumDecimalPrecision);

    public sealed record GskuCreateOptionsDto(
        IReadOnlyList<GskuCreateGlobalProductOptionDto> GlobalProducts,
        IReadOnlyList<GskuCreateUomOptionDto> Uoms);

    public sealed class CreateFinishedGoodDraftRequest
    {
        public Guid GskuId { get; init; }
        public string IdempotencyKey { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record FinishedGoodDraftDto(
        Guid FinishedGoodId,
        string CanonicalCode,
        Guid GskuId,
        string GskuCanonicalCode,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        CodeReservationBindingState CodeBindingState,
        bool BindingReconciliationRequired);

    public sealed record FinishedGoodListItemDto(
        Guid Id,
        string CanonicalCode,
        Guid GskuId,
        string GskuCanonicalCode,
        [property: JsonIgnore] string GskuDisplay,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record FinishedGoodDetailDto(
        Guid Id,
        string CanonicalCode,
        Guid GskuId,
        string GskuCanonicalCode,
        [property: JsonIgnore] string GskuDisplay,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record FinishedGoodGskuSelectorDto(
        Guid Id,
        [property: JsonIgnore] string CanonicalCode,
        [property: JsonIgnore] string Display)
    {
        [JsonPropertyName("gskuCanonicalCode")]
        public string GskuCanonicalCode => CanonicalCode;
    }

    public sealed class CreateLskuDraftRequest
    {
        public Guid GskuId { get; init; }
        public string MarketCode { get; init; } = string.Empty;
        public string IdempotencyKey { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record LskuDraftDto(
        Guid LskuId,
        string CanonicalCode,
        Guid GskuId,
        string MarketCode,
        ReferenceCatalogSelectionDto MarketSelection,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        CodeReservationBindingState CodeBindingState,
        bool BindingReconciliationRequired);

    public sealed record LskuListItemDto(
        Guid Id,
        string CanonicalCode,
        Guid GskuId,
        string GskuCanonicalCode,
        string MarketCode,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record LskuDetailDto(
        Guid Id,
        string CanonicalCode,
        Guid GskuId,
        string GskuCanonicalCode,
        string MarketCode,
        ProductIdentityLifecycleStatus LifecycleStatus,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record LskuCreateGskuOptionDto(
        Guid Id,
        string CanonicalCode,
        string GlobalProductCanonicalCode,
        string GlobalProductName,
        string RevisionIdentifier,
        decimal PackQuantity,
        string PackUomCode);

    public sealed record LskuCreateMarketOptionDto(
        string Code,
        string DisplayText,
        int SortOrder);

    public sealed record LskuCreateOptionsDto(
        IReadOnlyList<LskuCreateGskuOptionDto> Gskus,
        IReadOnlyList<LskuCreateMarketOptionDto> Markets);
}
