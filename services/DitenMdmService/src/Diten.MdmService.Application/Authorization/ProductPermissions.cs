namespace Diten.MdmService.Application.Authorization;

public static class ProductPermissions
{
    public static class Products
    {
        public const string Read = "Modules.Products.Read";
        public const string Create = "Modules.Products.Create";
        public const string Update = "Modules.Products.Update";
        public const string Delete = "Modules.Products.Delete";
        public const string BulkDelete = "Modules.Products.BulkDelete";
        public const string Patch = "Modules.Products.Patch";
        public const string Restore = "Modules.Products.Restore";
    }
}
