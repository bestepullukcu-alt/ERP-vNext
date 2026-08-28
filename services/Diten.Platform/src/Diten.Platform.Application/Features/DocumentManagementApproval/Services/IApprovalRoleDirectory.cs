namespace Diten.Platform.Application.Features.DocumentManagementApproval.Services;

public sealed record ApprovalDirectoryRole(Guid Id, string Name, string DisplayName);

public interface IApprovalRoleDirectory
{
    Task<IReadOnlyDictionary<string, ApprovalDirectoryRole>> ResolveAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken ct = default);

    Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
}
