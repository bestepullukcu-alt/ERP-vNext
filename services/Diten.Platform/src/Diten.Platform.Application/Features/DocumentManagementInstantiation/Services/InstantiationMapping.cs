using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;

internal static class InstantiationMapping
{
    public static InstantiationOutcomeModel ToModel(InstantiationOutcome outcome) =>
        new(
            outcome.NodeKey,
            outcome.CanonicalId,
            outcome.Status.ToWire(),
            outcome.ReasonCode,
            outcome.Message,
            outcome.Retryable);

    public static CollectionInstanceListItemModel ToListItem(CollectionInstance instance) =>
        new(
            instance.Id,
            instance.InstanceKey,
            instance.CompanyId,
            instance.BaselineReleaseId,
            instance.CanonicalId,
            instance.ParentCanonicalId,
            instance.Name,
            instance.FullPath,
            instance.DisplayOrder,
            instance.InstanceStatus.ToString().ToUpperInvariant(),
            instance.InstanceToken,
            instance.LastChangeAt,
            instance.VersionToken);

    public static CollectionInstanceDetailModel ToDetail(CollectionInstance instance) =>
        new(
            instance.Id,
            instance.InstanceKey,
            instance.CompanyId,
            instance.BaselineReleaseId,
            instance.CanonicalId,
            instance.ParentCanonicalId,
            instance.Name,
            instance.FullPath,
            instance.DisplayOrder,
            instance.CollectionScopeType.ToString().ToUpperInvariant(),
            instance.InstanceStatus.ToString().ToUpperInvariant(),
            instance.ScopeBindings.Select(ToScopeBinding).ToList(),
            instance.InstanceToken,
            instance.SourceDefinitionHash,
            instance.LastChangeAt,
            instance.VersionToken);

    private static ScopeBindingModel ToScopeBinding(ScopeBinding binding) =>
        new(
            binding.OrgBindingScopeType.ToString().ToUpperInvariant(),
            binding.OrgBindingScopeId,
            binding.ScopeSourceModule,
            binding.BindingStatus.ToString().ToUpperInvariant(),
            binding.EffectiveFrom,
            binding.EffectiveTo,
            binding.LastValidatedAt);
}
