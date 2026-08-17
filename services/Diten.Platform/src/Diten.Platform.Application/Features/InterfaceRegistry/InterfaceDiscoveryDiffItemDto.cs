using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceDiscoveryDiffItemDto(
    Guid DiffItemId,
    Guid BatchId,
    string InterfaceCode,
    string InterfaceVersion,
    string? EndpointKey,
    InterfaceChangeType ChangeType,
    string ReviewStatus,
    InterfaceReviewDecision? Decision,
    string? ReviewReason,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewedBy,
    string? PreviousHash,
    string? IncomingHash);
