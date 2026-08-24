namespace Diten.MdmService.Application.Contracts.ReferenceData;

public interface IVerifiedGskuReferenceResolver
{
    Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
        string packApplicabilityValueCode,
        string uomValueCode,
        CancellationToken cancellationToken = default);

    Task<VerifiedGskuUomEnumerationResult> EnumerateUomsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(VerifiedGskuUomEnumerationResult.Fail(
            503,
            "REFERENCE_PROVIDER_ENUMERATION_NOT_IMPLEMENTED"));
}

public sealed record VerifiedGskuUom(
    string Code,
    string DisplayText,
    int SortOrder,
    int MaximumDecimalPrecision);

public sealed record VerifiedGskuUomEnumerationResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    IReadOnlyList<VerifiedGskuUom> Uoms)
{
    public static VerifiedGskuUomEnumerationResult Success(IReadOnlyList<VerifiedGskuUom> uoms) =>
        new(true, 200, null, uoms);

    public static VerifiedGskuUomEnumerationResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, []);
}

public sealed record VerifiedGskuReferenceSelection(
    string SetCode,
    string ValueCode,
    Guid CatalogVersionId,
    int CatalogVersionNumber,
    string ResolutionMode,
    DateTimeOffset ResolvedAtUtc,
    bool IsRetired,
    bool SelectableForNew);

public sealed record VerifiedGskuReferenceResolveResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    IReadOnlyList<VerifiedGskuReferenceSelection> Selections)
{
    public static VerifiedGskuReferenceResolveResult Success(
        IReadOnlyList<VerifiedGskuReferenceSelection> selections) => new(true, 200, null, selections);

    public static VerifiedGskuReferenceResolveResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, []);
}
