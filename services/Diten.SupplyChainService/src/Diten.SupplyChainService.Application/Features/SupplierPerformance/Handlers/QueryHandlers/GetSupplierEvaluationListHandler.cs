using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.QueryHandlers;
public sealed class GetSupplierEvaluationListHandler(IEvaluationRepository repository, SupplierPerformanceRequestContext context) : IRequestHandler<GetSupplierEvaluationListQuery, Response<EvaluationListResponse>>
{
    public async Task<Response<EvaluationListResponse>> Handle(GetSupplierEvaluationListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = request.Page is null ? 1 : int.Parse(request.Page, System.Globalization.CultureInfo.InvariantCulture);
        var size = request.PageSize is null ? SupplierPerformanceWire.DefaultPageSize : int.Parse(request.PageSize, System.Globalization.CultureInfo.InvariantCulture);
        // supplierId matches the stored canonical code exactly; a legacy code finds nothing and Procurement is not asked (§0.0 D2).
        var filter = new EvaluationFilter(request.SupplierId, request.Status is null ? null : Enum.Parse<EvaluationStatus>(request.Status), request.PeriodFrom, request.PeriodTo);
        var (items, total) = await repository.ListAsync(context.Scope, filter, page, size, ct);
        return Response<EvaluationListResponse>.Success(new(items.Select(EvaluationResponse.From).ToList(), page, size, total, context.CorrelationId));
    }
}
