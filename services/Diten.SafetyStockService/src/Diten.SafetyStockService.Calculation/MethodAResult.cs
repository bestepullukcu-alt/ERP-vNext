namespace Diten.SafetyStockService.Calculation;

public sealed record MethodAResult
{
    private MethodAResult(
        decimal? candidateQuantity,
        string? baseUom,
        MethodATrace? trace,
        MethodAError? error,
        string? errorDetail)
    {
        CandidateQuantity = candidateQuantity;
        BaseUom = baseUom;
        Trace = trace;
        Error = error;
        ErrorDetail = errorDetail;
    }

    public bool IsSuccess => Error is null;
    public decimal? CandidateQuantity { get; }
    public string? BaseUom { get; }
    public MethodATrace? Trace { get; }
    public MethodAError? Error { get; }
    public string? ErrorDetail { get; }

    internal static MethodAResult Success(MethodATrace trace) =>
        new(trace.RawCandidateQuantity, trace.BaseUom, trace, null, null);

    internal static MethodAResult Failure(MethodAError error, string detail) =>
        new(null, null, null, error, detail);
}
