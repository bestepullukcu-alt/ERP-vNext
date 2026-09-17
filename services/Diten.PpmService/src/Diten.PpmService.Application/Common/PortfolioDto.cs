using System.Text.Json.Serialization;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Application.Features.Portfolios;

namespace Diten.PpmService.Application.Common;

public sealed record PortfolioDto(Guid Id, string Code, string Name, string? Description,
    PortfolioLifecycleState LifecycleState, string? VisibilityPolicyKey, bool IsReferenceable, int Version)
{
    public string? CapacityAllocationDescription { get; init; }
    public PortfolioActions Actions { get; init; } = new(false, false);
    public bool OwnerVisible { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PortfolioOwnerSummary? Owner { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PortfolioOwnerHistoryItem>? OwnerHistory { get; init; }
}
