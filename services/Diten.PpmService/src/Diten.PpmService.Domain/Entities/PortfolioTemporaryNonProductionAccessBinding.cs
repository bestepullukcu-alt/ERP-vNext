namespace Diten.PpmService.Domain.Entities;

// Embedded in Portfolio, never addressed independently, and bound to its aggregate Id.
public sealed record PortfolioTemporaryNonProductionAccessBinding(
    string DecisionKey,
    string DecisionVersion,
    DateTime BoundAtUtc,
    Guid PortfolioId)
{
    public bool HasValidShape() =>
        !string.IsNullOrWhiteSpace(DecisionKey) && DecisionKey.Length <= 128 &&
        !string.IsNullOrWhiteSpace(DecisionVersion) && DecisionVersion.Length <= 64 &&
        BoundAtUtc != default && BoundAtUtc.Kind == DateTimeKind.Utc && PortfolioId != Guid.Empty;
}
