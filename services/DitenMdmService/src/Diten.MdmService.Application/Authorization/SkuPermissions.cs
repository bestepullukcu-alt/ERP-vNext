namespace Diten.MdmService.Application.Authorization;

public static class SkuPermissions
{
    public const string GroupName = "Modules.Skus";

    public static class Skus
    {
        public const string View = $"{GroupName}.View";
        public const string Create = $"{GroupName}.Create";
        public const string Edit = $"{GroupName}.Edit";
        public const string Delete = $"{GroupName}.Delete";
        public const string BulkDelete = $"{GroupName}.BulkDelete";
    }
}
