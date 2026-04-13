using Diten.Application.Dtos.Decomposition;

namespace Diten.Application.Services.Decomposition;

public interface IDecompositionService
{
    Task<DecompositionStructureDto> CreateStructureAsync(CreateStructureRequest request, string? actor, CancellationToken ct = default);
    Task<DecompositionStructureDto?> GetStructureAsync(string structureId, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> UpdateStructureAsync(string structureId, UpdateStructureRequest request, string? actor, CancellationToken ct = default);

    Task<(DecompositionStructureDto? dto, string? error)> CreateNodeAsync(string structureId, CreateNodeRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> UpdateNodeAsync(string nodeId, UpdateNodeRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> DeleteNodeAsync(string nodeId, int expectedVersion, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> AddChildAsync(string nodeId, CreateNodeRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> AddSiblingAsync(string nodeId, AddSiblingRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> MoveNodeAsync(string nodeId, MoveNodeRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> ReorderNodeAsync(string nodeId, ReorderNodeRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> AddDependencyAsync(string structureId, AddDependencyRequest request, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> DeleteDependencyAsync(string dependencyId, int expectedVersion, string? actor, CancellationToken ct = default);

    Task<(DecompositionStructureDto? dto, string? error)> ValidateStructureAsync(string structureId, string? actor, CancellationToken ct = default);
    Task<(DecompositionStructureDto? dto, string? error)> ApproveStructureAsync(string structureId, int expectedVersion, string? actor, CancellationToken ct = default);
    Task<IReadOnlyList<DecompositionValidationIssueDto>> GetIssuesAsync(string structureId, CancellationToken ct = default);
    Task<IReadOnlyList<DecompositionAuditEventDto>> GetHistoryAsync(string structureId, CancellationToken ct = default);
}
