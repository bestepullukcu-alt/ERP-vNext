namespace Diten.MdmService.Application.Authorization;

public static class ItemPermissions
{
    public static class Items
    {
        public const string Read = "Modules.Items.Read";
        public const string Create = "Modules.Items.Create";
        public const string Update = "Modules.Items.Update";
        public const string Delete = "Modules.Items.Delete";
        public const string BulkDelete = "Modules.Items.BulkDelete";
        public const string Patch = "Modules.Items.Patch";

        public static readonly string[] All =
        [
            Read,
            Create,
            Update,
            Delete,
            BulkDelete,
            Patch
        ];
    }

    public static class ItemCategories
    {
        public const string Read = "Modules.ItemCategories.Read";
        public const string Create = "Modules.ItemCategories.Create";
        public const string Update = "Modules.ItemCategories.Update";
        public const string Delete = "Modules.ItemCategories.Delete";
        public const string BulkDelete = "Modules.ItemCategories.BulkDelete";

        public static readonly string[] All =
        [
            Read,
            Create,
            Update,
            Delete,
            BulkDelete
        ];
    }

    public static class ItemVariantModels
    {
        public const string Read = "Modules.ItemVariantModels.Read";
        public const string Create = "Modules.ItemVariantModels.Create";
        public const string Update = "Modules.ItemVariantModels.Update";
        public const string Delete = "Modules.ItemVariantModels.Delete";
        public const string BulkDelete = "Modules.ItemVariantModels.BulkDelete";

        public static readonly string[] All =
        [
            Read,
            Create,
            Update,
            Delete,
            BulkDelete
        ];
    }
}
