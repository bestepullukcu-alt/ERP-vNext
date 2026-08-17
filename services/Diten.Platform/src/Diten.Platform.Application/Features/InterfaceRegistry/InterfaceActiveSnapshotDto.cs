using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceActiveSnapshotDto(
    string InterfaceCode,
    string InterfaceVersion,
    string SnapshotHash,
    InterfaceLifecycleStatus LifecycleStatus,
    DateTimeOffset ConfirmedAtUtc,
    string? ConfirmedBy,
    string? DeprecationReason,
    DateTimeOffset? DeprecatedAtUtc,
    string? DeprecatedBy,
    InterfaceDefinitionDto Definition);
