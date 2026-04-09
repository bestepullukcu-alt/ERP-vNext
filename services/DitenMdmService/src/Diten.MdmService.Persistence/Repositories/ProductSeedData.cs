using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Persistence.Repositories;

internal static class ProductSeedData
{
    internal static readonly Guid DraftLifecycleStateId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    internal static readonly Guid CnsCategoryId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    internal static readonly Guid PediatricCategoryId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    internal static readonly Guid UrologyCategoryId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    internal static readonly Guid AntiInfectiveCategoryId = Guid.Parse("60000000-0000-0000-0000-000000000004");
    internal static readonly Guid SupplementCategoryId = Guid.Parse("60000000-0000-0000-0000-000000000005");
    internal static readonly Guid ManufacturingServiceCategoryId = Guid.Parse("60000000-0000-0000-0000-00000000000D");
    internal static readonly Guid DigitalSolutionCategoryId = Guid.Parse("60000000-0000-0000-0000-000000000010");

    private sealed record ProductSeedDefinition(Guid Id, string Code, string Name, ProductType ProductType, Guid CategoryId);

    private static readonly ProductSeedDefinition[] Definitions =
    [
        new(Guid.Parse("70000000-0000-0000-0000-000000000001"), "PRX-NIGHT", "Parion-X Night", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000002"), "PRX-BOON", "Parion-X Boonmood", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000003"), "PRX-DAY", "Parion-X Day", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000004"), "PRX-DEEP", "Parion-X Deep", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000005"), "PRX-FOCUS", "Parion-X Focusmax", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000006"), "PRX-JUNIOR", "Parion-X Junior", ProductType.FinishedProduct, PediatricCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000007"), "PRX-MIGRA", "Parion-X Migra", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000008"), "PRX-MIND", "Parion-X Mindcontrol", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000009"), "PRX-SOMNA", "Parion-X Somnabust", ProductType.FinishedProduct, CnsCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-00000000000A"), "PRX-VITA", "Parion-X Vitacare", ProductType.FinishedProduct, SupplementCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-00000000000B"), "CYS", "Cystoliberin", ProductType.FinishedProduct, UrologyCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-00000000000C"), "CYS-PLUS", "Cystoliberin Plus", ProductType.FinishedProduct, UrologyCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-00000000000D"), "CYS-JUNIOR", "Cystoliberin Junior", ProductType.FinishedProduct, PediatricCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-00000000000E"), "ALM", "Almiba", ProductType.FinishedProduct, AntiInfectiveCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-00000000000F"), "ALM-PLUS", "Almiba Plus", ProductType.FinishedProduct, AntiInfectiveCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000010"), "GM-CMO-TAB", "Tablet Manufacturing Service", ProductType.Service, ManufacturingServiceCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000011"), "GM-CMO-INJ", "Injection Manufacturing Service", ProductType.Service, ManufacturingServiceCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000012"), "MYG-SUS", "Oral Suspension Manufacturing", ProductType.Service, ManufacturingServiceCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000013"), "DTN-ERP", "Diten ERP Platform", ProductType.Technology, DigitalSolutionCategoryId),
        new(Guid.Parse("70000000-0000-0000-0000-000000000014"), "DTN-ANALYTICS", "Diten Analytics Suite", ProductType.Technology, DigitalSolutionCategoryId)
    ];

    internal static IReadOnlyList<Product> BuildProducts()
    {
        return Definitions.Select(definition => new Product
        {
            Id = definition.Id,
            Code = definition.Code,
            Name = definition.Name,
            ProductType = definition.ProductType,
            CategoryId = definition.CategoryId,
            LifecycleStateId = DraftLifecycleStateId,
            IsSaleable = definition.ProductType is ProductType.FinishedProduct or ProductType.Service or ProductType.Technology,
            IsPurchasable = false,
            IsManufacturable = definition.ProductType == ProductType.FinishedProduct
        }).ToList();
    }
}
