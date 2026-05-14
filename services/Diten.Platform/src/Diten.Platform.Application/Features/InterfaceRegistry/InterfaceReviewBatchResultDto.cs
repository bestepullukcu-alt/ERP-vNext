namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceReviewBatchResultDto(
    InterfaceDiscoveryBatchDto Batch,
    IReadOnlyList<InterfaceDiscoveryDiffItemDto> DiffItems);
