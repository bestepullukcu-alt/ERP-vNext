using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IDomainLandscapeRepository
{
    Task<IReadOnlyList<DomainLandscape>> GetAllAsync(CancellationToken ct = default);
    Task<DomainLandscape?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DomainLandscape?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<DomainLandscape> CreateAsync(DomainLandscape entity, CancellationToken ct = default);
    Task UpdateAsync(DomainLandscape entity, CancellationToken ct = default);
}

public interface ISuitePlatformRepository
{
    Task<IReadOnlyList<SuitePlatform>> GetAllAsync(CancellationToken ct = default);
    Task<SuitePlatform?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SuitePlatform?> GetByCodeAsync(Guid domainLandscapeId, string code, CancellationToken ct = default);
    Task<SuitePlatform> CreateAsync(SuitePlatform entity, CancellationToken ct = default);
    Task UpdateAsync(SuitePlatform entity, CancellationToken ct = default);
}

public interface ICapabilityGroupRepository
{
    Task<IReadOnlyList<CapabilityGroup>> GetAllAsync(CancellationToken ct = default);
    Task<CapabilityGroup?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CapabilityGroup?> GetByCodeAsync(Guid suitePlatformId, string code, CancellationToken ct = default);
    Task<CapabilityGroup> CreateAsync(CapabilityGroup entity, CancellationToken ct = default);
    Task UpdateAsync(CapabilityGroup entity, CancellationToken ct = default);
}

public interface IModuleDefinitionRepository
{
    Task<IReadOnlyList<ModuleDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<ModuleDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ModuleDefinition?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default);
    Task<(IReadOnlyList<ModuleDefinition> Items, long TotalCount)> QueryAsync(ModuleDefinitionQuery query, CancellationToken ct = default);
    Task<ModuleDefinition> CreateAsync(ModuleDefinition entity, CancellationToken ct = default);
    Task UpdateAsync(ModuleDefinition entity, CancellationToken ct = default);
}

public interface IModulePageDefinitionRepository
{
    Task<IReadOnlyList<ModulePageDefinition>> GetByModuleIdAsync(string moduleId, CancellationToken ct = default);
    Task<ModulePageDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ModulePageDefinition?> GetByCodeAsync(string moduleId, string pageCode, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string moduleId, string pageCode, Guid? excludeId = null, CancellationToken ct = default);
    Task<ModulePageDefinition> CreateAsync(ModulePageDefinition entity, CancellationToken ct = default);
    Task UpdateAsync(ModulePageDefinition entity, CancellationToken ct = default);
}

public sealed record ModuleDefinitionQuery(
    string? Search,
    Guid? DomainLandscapeId,
    Guid? SuitePlatformId,
    Guid? CapabilityGroupId,
    string? Status,
    bool? IsTenantAssignable,
    bool? IsPlatformCore);
