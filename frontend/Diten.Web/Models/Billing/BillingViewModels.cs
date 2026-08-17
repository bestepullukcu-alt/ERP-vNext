using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Billing;

public sealed class BillingInvoiceEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public Guid? BillingPlanId { get; set; }

    [Required]
    [StringLength(160)]
    public string CustomerReference { get; set; } = string.Empty;

    public DateTimeOffset? DueDate { get; set; }

    public string? Notes { get; set; }

    [Required]
    [StringLength(240)]
    public string LineDescription { get; set; } = string.Empty;

    [Required]
    [Range(0, 999999999)]
    public decimal? Quantity { get; set; }

    [Required]
    [Range(0, 999999999)]
    public decimal? UnitPrice { get; set; }

    [Range(0, 999999999)]
    public decimal? DiscountAmount { get; set; }

    [Range(0, 999999999)]
    public decimal? TaxAmount { get; set; }
}

public sealed class BillingInvoiceDetailViewModel
{
    public Guid Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid BillingPlanId { get; set; }
    public int BillingPlanVersion { get; set; }
    public string BillingPlanNameSnapshot { get; set; } = string.Empty;
    public decimal BillingPlanAmountSnapshot { get; set; }
    public string BillingPlanCurrencySnapshot { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset? DueDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public List<BillingInvoiceLineViewModel> Lines { get; set; } = [];
}

public sealed class BillingInvoiceLineViewModel
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class BillingPlanOptionViewModel
{
    public Guid Id { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public int PlanVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string BillingInterval { get; set; } = string.Empty;
}

public sealed class BillingInvoiceSavePayload
{
    public Guid BillingPlanId { get; set; }
    public string CustomerReference { get; set; } = string.Empty;
    public DateTimeOffset? DueDate { get; set; }
    public string? Notes { get; set; }
    public List<BillingInvoiceLineSavePayload> Lines { get; set; } = [];
}

public sealed class BillingInvoiceLineSavePayload
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
}

public sealed class BillingGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
}
