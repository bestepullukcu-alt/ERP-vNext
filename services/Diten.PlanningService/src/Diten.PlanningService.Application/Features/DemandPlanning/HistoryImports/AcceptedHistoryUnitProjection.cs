using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public static class AcceptedHistoryUnitProjection
{
    public static async Task<AcceptedDemandHistoryView> ResolveAsync(
        AcceptedDemandHistoryView view, IHistoryRowReferenceChecker checker,
        CancellationToken cancellationToken)
    {
        if (!view.Row.UnitVerificationPending) return view;
        var status = await CheckAsync(view.Row, checker, cancellationToken);
        return view with
        {
            EffectiveUnitVerificationPending = status != "VerifiedBase",
            UnitWarning = status == "VerifiedBase" ? null : status
        };
    }

    public static async Task<string> CheckAsync(AcceptedDemandHistoryRow row,
        IHistoryRowReferenceChecker checker, CancellationToken cancellationToken)
    {
        if (!row.UnitVerificationPending) return "VerifiedBase";
        try
        {
            var reference = await checker.CheckAsync(row.TenantId, row.LegalEntityId,
                row.SkuId, row.SkuLevel, row.WarehouseId, row.OccurredOn,
                row.OriginalUomId, cancellationToken);
            if (reference.State != HistoryReferenceState.Verified ||
                reference.UnitVerificationPending) return "UnitVerificationPending";
            return reference.BaseUomId == row.OriginalUomId ? "VerifiedBase" :
                "DifferentFromCurrentBase";
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        {
            return "UnitVerificationPending";
        }
    }
}
