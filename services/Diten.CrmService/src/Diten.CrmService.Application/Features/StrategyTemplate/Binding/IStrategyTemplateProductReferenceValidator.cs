namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

/// <summary>
/// MOD-0167 FU04 cross-service contract: proves that an MDM reference bound by a product line EXISTS, before the
/// template is written. It derives nothing and reads no business data — the only question it asks is "does this id
/// exist?".
/// <para><b>404 and "cannot answer" are different outcomes on purpose.</b> <see cref="Outcome.NotFound"/> means the
/// dependency spoke and the binding is not authorable (400). <see cref="Outcome.Unavailable"/> means we do not know —
/// a timeout, a 5xx, an auth rejection or a malformed body — and that is a 503 with nothing persisted.</para>
/// <para>Brand is deliberately absent from <see cref="ReferenceKind"/>: the product does not use brands (D-BRAND), so
/// there is no brand path to consume and no brand id to prove.</para>
/// </summary>
public interface IStrategyTemplateProductReferenceValidator
{
    public enum Outcome
    {
        Valid,
        NotFound,
        Unavailable
    }

    /// <summary>The two MDM masters a product line binds. There is no third, and adding one is a pack decision, not an
    /// implementation detail.</summary>
    public static class ReferenceKind
    {
        public const string GlobalProduct = "global-product";
        public const string Gsku = "gsku";
    }

    Task<Outcome> ValidateAsync(string referenceKind, Guid referenceId, CancellationToken cancellationToken);
}
