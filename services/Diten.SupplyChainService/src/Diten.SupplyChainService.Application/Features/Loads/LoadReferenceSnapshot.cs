namespace Diten.SupplyChainService.Application.Features.Loads;
public sealed record LoadReferenceSnapshot(string Kind, DateTimeOffset ObservedAt, string Response);
