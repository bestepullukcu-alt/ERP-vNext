using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;

namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU01 — maps API request payloads to Application input records (no business logic).
internal static class ApiRequestMapper
{
    public static FileUploadInput ToFileInput(FileUploadApiInput? file) =>
        new(file?.FileName ?? string.Empty, file?.MediaType, file?.ContentBase64 ?? string.Empty);

    public static DocumentAccessPolicyInput? ToAccessPolicy(AccessPolicyApiInput? input)
    {
        if (input is null)
        {
            return null;
        }

        return new DocumentAccessPolicyInput(
            input.Source,
            input.Grants.Select(g => new AccessGrantInput(g.Action, g.TargetType, g.TargetId)).ToList());
    }

    public static TemplateFlagsInput? ToFlags(TemplateFlagsApiInput? input) =>
        input is null ? null : new TemplateFlagsInput(input.Reusable, input.Shareable, input.CopyableOnAdopt, input.ReferenceOnly);

    public static FolderPermissionsInput ToFolderPermissions(FolderPermissionsApiInput input) =>
        new(input.CanViewFolderDocuments, input.CanUploadDocument, input.CanEditFolderDocuments,
            input.CanUploadNewVersion, input.CanShareFolderDocuments, input.CanManageFolderDocumentAccess);
}
