namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
/// <summary>A contract error decided below the handler (eligibility read, persistence); DetailCode becomes error.details.code.</summary>
public sealed class SupplierPerformanceFailure(int status, string code, string? detailCode = null) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string? DetailCode { get; } = detailCode;
}
