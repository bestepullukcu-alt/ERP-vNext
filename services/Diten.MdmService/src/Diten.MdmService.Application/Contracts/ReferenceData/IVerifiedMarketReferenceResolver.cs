namespace Diten.MdmService.Application.Contracts.ReferenceData;

public interface IVerifiedMarketReferenceResolver
{
    Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
        string marketCode,
        CancellationToken cancellationToken = default);

    Task<VerifiedMarketEnumerationResult> EnumerateActiveAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(VerifiedMarketEnumerationResult.Fail(
            503,
            "REFERENCE_PROVIDER_UNAVAILABLE"));
}

public sealed record VerifiedMarketReferenceSelection(
    string SetCode,
    string ValueCode,
    Guid CatalogVersionId,
    int CatalogVersionNumber,
    string ResolutionMode,
    DateTimeOffset ResolvedAtUtc);

public sealed record VerifiedMarketReferenceResolveResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    VerifiedMarketReferenceSelection? Selection)
{
    public static VerifiedMarketReferenceResolveResult Success(VerifiedMarketReferenceSelection selection) =>
        new(true, 200, null, selection);

    public static VerifiedMarketReferenceResolveResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, null);
}

public sealed record VerifiedMarketOption(
    string Code,
    string DisplayText,
    int SortOrder);

public sealed record VerifiedMarketEnumerationResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    IReadOnlyList<VerifiedMarketOption> Markets)
{
    public static VerifiedMarketEnumerationResult Success(IReadOnlyList<VerifiedMarketOption> markets) =>
        new(true, 200, null, markets);

    public static VerifiedMarketEnumerationResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, []);
}
