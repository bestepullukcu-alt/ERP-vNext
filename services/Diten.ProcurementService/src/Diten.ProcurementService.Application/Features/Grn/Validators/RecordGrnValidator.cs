using Diten.ProcurementService.Application.Features.Grn.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Grn.Validators;

/// <summary>
/// recordGrn validation (MOD-0142 §12): warehouseId zorunlu; ≥1 satır; her satır itemId+skuId+uomId zorunlu,
/// skuLevel &amp; toStockStatus geçerli enum, quantity Decimal &amp; > 0 (float reddi). (İş kuralı 422 handler'da da
/// zorlanır — contract GrnUnprocessable. inventoryTransactionId/lotId girdi DEĞİL; INVENTORY post sonucu.)
/// </summary>
public sealed class RecordGrnValidator : AbstractValidator<RecordGrnCommand>
{
    public RecordGrnValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("warehouseId zorunlu");

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("en az bir satır zorunlu")
            .Must(l => l is not null && l.Count > 0).WithMessage("en az bir satır zorunlu");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty().WithMessage("line.itemId zorunlu");
            line.RuleFor(l => l.SkuId).NotEmpty().WithMessage("line.skuId zorunlu");
            line.RuleFor(l => l.UomId).NotEmpty().WithMessage("line.uomId zorunlu");
            line.RuleFor(l => l.SkuLevel)
                .Must(TryParseSkuLevel).WithMessage("skuLevel geçersiz (Gsku/Lsku/FinishedGood)");
            line.RuleFor(l => l.ToStockStatus)
                .Must(TryParseStockStatus).WithMessage("toStockStatus geçersiz (AVAILABLE/QUALITY_INSPECTION/QUARANTINE)");
            line.RuleFor(l => l.Quantity)
                .Must(GrnValidationRules.IsValidDecimal).WithMessage("quantity Decimal string olmalı (float YASAK)")
                .Must(GrnValidationRules.IsPositiveDecimal).WithMessage("quantity > 0 olmalı");
        });
    }

    private static bool TryParseSkuLevel(string? v) => GrnValidationRules.TryParseSkuLevel(v, out _);
    private static bool TryParseStockStatus(string? v) => GrnValidationRules.TryParseStockStatus(v, out _);
}
