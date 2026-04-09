using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Products;

internal static class ProductCatalog
{
    internal sealed record ProductTypeDefinition(ProductType Value, string Code, string Name);

    internal sealed record ProductCategoryDefinition(Guid Id, ProductType ProductType, string Code, string Name);

    private static readonly ProductTypeDefinition[] ProductTypes =
    [
        new(ProductType.FinishedProduct, "FINISHED_PRODUCT", "Finished Product"),
        new(ProductType.SemiFinishedProduct, "SEMI_FINISHED_PRODUCT", "Semi Finished Product"),
        new(ProductType.Service, "SERVICE", "Service"),
        new(ProductType.Technology, "TECHNOLOGY", "Technology")
    ];

    private static readonly ProductCategoryDefinition[] Categories =
    [
        new(Guid.Parse("60000000-0000-0000-0000-000000000001"), ProductType.FinishedProduct, "CNS", "CNS"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000002"), ProductType.FinishedProduct, "PEDIATRIC", "Pediatric"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000003"), ProductType.FinishedProduct, "UROLOGY", "Urology"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000004"), ProductType.FinishedProduct, "ANTI_INFECTIVE", "Anti-Infective"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000005"), ProductType.FinishedProduct, "SUPPLEMENT", "Supplement"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000006"), ProductType.FinishedProduct, "DISINFECTANT", "Disinfectant"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000007"), ProductType.SemiFinishedProduct, "CNS", "CNS"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000008"), ProductType.SemiFinishedProduct, "PEDIATRIC", "Pediatric"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000009"), ProductType.SemiFinishedProduct, "UROLOGY", "Urology"),
        new(Guid.Parse("60000000-0000-0000-0000-00000000000A"), ProductType.SemiFinishedProduct, "ANTI_INFECTIVE", "Anti-Infective"),
        new(Guid.Parse("60000000-0000-0000-0000-00000000000B"), ProductType.SemiFinishedProduct, "SUPPLEMENT", "Supplement"),
        new(Guid.Parse("60000000-0000-0000-0000-00000000000C"), ProductType.SemiFinishedProduct, "DISINFECTANT", "Disinfectant"),
        new(Guid.Parse("60000000-0000-0000-0000-00000000000D"), ProductType.Service, "MANUFACTURING_SERVICE", "Manufacturing Service"),
        new(Guid.Parse("60000000-0000-0000-0000-00000000000E"), ProductType.Service, "CONSULTING", "Consulting"),
        new(Guid.Parse("60000000-0000-0000-0000-00000000000F"), ProductType.Service, "SUPPORT", "Support"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000010"), ProductType.Technology, "DIGITAL_SOLUTION", "Digital Solution"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000011"), ProductType.Technology, "PLATFORM", "Platform"),
        new(Guid.Parse("60000000-0000-0000-0000-000000000012"), ProductType.Technology, "INTEGRATION", "Integration")
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
