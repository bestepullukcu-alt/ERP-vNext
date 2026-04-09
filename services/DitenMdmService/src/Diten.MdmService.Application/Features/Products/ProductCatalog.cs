using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Products;

internal static class ProductCatalog
{
    internal sealed record ProductTypeDefinition(ProductType Value, string Code, string Name);

    internal sealed record ProductCategoryDefinition(Guid Id, ProductType ProductType, string Code, string Name);

    private static readonly ProductTypeDefinition[] ProductTypes =
    [
        new(ProductType.FinishedGood, "FINISHED_GOOD", "Finished Good"),
        new(ProductType.Service, "SERVICE", "Service"),
        new(ProductType.Digital, "DIGITAL", "Digital")
    ];

    private static readonly ProductCategoryDefinition[] Categories =
    [
        new(Guid.Parse("60000000-0000-0000-0000-000000000001"), ProductType.FinishedGood, "STANDARD", "Standard"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000002"), ProductType.FinishedGood, "REGULATED", "Regulated"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000003"), ProductType.Service, "PROFESSIONAL", "Professional"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000004"), ProductType.Service, "SUPPORT", "Support"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000005"), ProductType.Digital, "LICENSE", "License"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000006"), ProductType.Digital, "SUBSCRIPTION", "Subscription")
    ];

    public static IReadOnlyList<ProductTypeDefinition> GetProductTypes() => ProductTypes;

    public static IReadOnlyList<ProductCategoryDefinition> GetCategories() => Categories;

    public static ProductTypeDefinition GetProductTypeDefinition(ProductType type)
    {
        return ProductTypes.First(x => x.Value == type);
    }

    public static ProductCategoryDefinition GetCategoryDefinition(Guid categoryId)
    {
        return Categories.First(x => x.Id == categoryId);
    }

    public static bool TryGetCategoryDefinition(Guid categoryId, out ProductCategoryDefinition category)
    {
        category = Categories.FirstOrDefault(x => x.Id == categoryId)!;
        return category is not null;
    }

    public static bool IsCategoryValidForProductType(Guid categoryId, ProductType productType)
    {
        return Categories.Any(x => x.Id == categoryId && x.ProductType == productType);
    }
}
