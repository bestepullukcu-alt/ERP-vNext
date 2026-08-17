using Diten.Platform.Application.Features.Billing.Commands;
using Diten.Platform.Domain.Enums.Billing;
using FluentValidation;

namespace Diten.Platform.Application.Features.Billing.Validators;

public sealed class CreateBillingPlanCommandValidator : AbstractValidator<CreateBillingPlanCommand>
{
    public CreateBillingPlanCommandValidator()
    {
        RuleFor(x => x.Request.PlanCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.BillingInterval).Must(BeBillingInterval);
    }

    private static bool BeBillingInterval(string value) => Enum.TryParse<BillingInterval>(value, true, out _);
}

public sealed class ReviseBillingPlanCommandValidator : AbstractValidator<ReviseBillingPlanCommand>
{
    public ReviseBillingPlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.BillingInterval).Must(value => Enum.TryParse<BillingInterval>(value, true, out _));
    }
}

public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.Request.BillingPlanId).NotEmpty();
        RuleFor(x => x.Request.CustomerReference).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.Lines).NotEmpty();
        RuleForEach(x => x.Request.Lines).SetValidator(new InvoiceLineRequestValidator());
    }
}

public sealed class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Request.CustomerReference).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.Lines).NotEmpty();
        RuleForEach(x => x.Request.Lines).SetValidator(new InvoiceLineRequestValidator());
    }
}

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.AppliedAmount).GreaterThan(0);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
    }
}

public sealed class CreateRefundCommandValidator : AbstractValidator<CreateRefundCommand>
{
    public CreateRefundCommandValidator()
    {
        RuleFor(x => x.PaymentRecordId).NotEmpty();
        RuleFor(x => x.Request.RefundAmount).GreaterThan(0);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(240);
    }
}

public sealed class InvoiceLineRequestValidator : AbstractValidator<InvoiceLineRequest>
{
    public InvoiceLineRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(240);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
    }
}
