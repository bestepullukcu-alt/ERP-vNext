---
id: PSS-006
name: Billing Management
domain: platform-shared-services
status: in-progress
owner: platform-team
branch: feature/pss/pss-006-billing-management
started: 2026-05-05
target: 2026-05-19
form_field_count: 9
golden_reference: compact
api_profile: proxy-profile
---

# PSS-006 — Billing Management

## Domain Model Changes

### BillingPlan
- Add `PlanCode: string`.
- Add `PlanVersion: int`.
- Add `Status: Draft | Active | Retired`.
- Add immutable pricing fields once `Status = Active`:
  - `Name`
  - `Amount`
  - `Currency`
  - `BillingInterval`
- Updating pricing creates a new `BillingPlan` document with:
  - same `TenantId`
  - same `PlanCode`
  - incremented `PlanVersion`
  - new pricing fields
- A `BillingPlan` version referenced by any invoice MUST NOT be mutated.

### Invoice
- `InvoiceNumber` is nullable while `Status = Draft`.
- `InvoiceNumber` is assigned only during `Draft -> Issued`.
- Add plan snapshot fields:
  - `BillingPlanId`
  - `BillingPlanVersion`
  - `BillingPlanNameSnapshot`
  - `BillingPlanAmountSnapshot`
  - `BillingPlanCurrencySnapshot`
- Monetary fields:
  - `SubTotal`
  - `TaxTotal`
  - `DiscountTotal`
  - `GrandTotal`
  - `PaidAmount`
  - `BalanceDue`
- All monetary fields use `decimal` with scale `2`.
- `GrandTotal = SubTotal + TaxTotal - DiscountTotal`.
- `BalanceDue = GrandTotal - PaidAmount`.
- `PaidAmount >= 0`.
- `BalanceDue >= 0`.

### InvoiceLine
- Add required `Currency`.
- `Currency` MUST equal parent invoice `Currency`.
- `Quantity >= 0`.
- `UnitPrice >= 0`.
- `DiscountAmount >= 0`.
- `TaxAmount >= 0`.
- `LineTotal >= 0`.

### PaymentRecord
- Add required `IdempotencyKey`.
- Add required `InvoiceId`.
- Add required `Currency`.
- Add required `AppliedAmount`.
- Add `RefundedAmount`.
- Add `Status: Applied | Reversed | PartiallyRefunded | Refunded`.
- `AppliedAmount > 0`.
- `RefundedAmount >= 0`.
- `RefundedAmount <= AppliedAmount`.
- `Currency` MUST equal invoice `Currency`.

### RefundRecord
- Add entity `RefundRecord`.
- Fields:
  - `Id`
  - `TenantId`
  - `InvoiceId`
  - `PaymentRecordId`
  - `RefundAmount`
  - `Currency`
  - `Reason`
  - `CreatedAt`
  - `CreatedBy`
- `RefundAmount > 0`.
- `RefundAmount <= PaymentRecord.AppliedAmount - PaymentRecord.RefundedAmount`.
- `Currency` MUST equal payment and invoice currency.

### BillingActivityEvent
- Move these fields out of metadata:
  - `InvoiceId`
  - `InvoiceNumber`
  - `PaymentRecordId`
  - `RefundRecordId`
  - `EventType`
  - `OccurredAt`
  - `ActorUserId`
- `Metadata` is allowed only for optional non-critical context.

## Lifecycle Updates

### Invoice Lifecycle
- Allowed statuses:
  - `Draft`
  - `Issued`
  - `PartiallyPaid`
  - `Paid`
  - `Cancelled`
- Draft invoice:
  - `InvoiceNumber = null`
  - editable fields allowed
  - no invoice sequence consumed
- Issue transition:
  - only valid from `Draft`
  - generates `InvoiceNumber`
  - consumes one tenant-year sequence atomically
  - sets `IssuedAt`
  - sets `Status = Issued`
  - freezes invoice content
- After issue:
  - invoice content is immutable
  - invoice lines are immutable
  - invoice totals are immutable except payment/refund-derived `PaidAmount`, `BalanceDue`, and `Status`
- Cancel:
  - allowed only when `Status = Issued` and `PaidAmount = 0`
  - rejected for `Draft`, `PartiallyPaid`, `Paid`, and `Cancelled`
  - sets `Status = Cancelled`

### Payment Lifecycle
- Payment can be created only for:
  - `Issued`
  - `PartiallyPaid`
- Payment cannot be applied to:
  - `Draft`
  - `Paid`
  - `Cancelled`
- Payment application updates invoice atomically:
  - increment `PaidAmount`
  - decrement `BalanceDue`
  - set `Status = Paid` when `BalanceDue = 0`
  - set `Status = PartiallyPaid` when `BalanceDue > 0`

### Refund Lifecycle
- Refund can be created only against an existing `PaymentRecord`.
- Refund updates invoice:
  - decrement `PaidAmount`
  - increment `BalanceDue`
- Status rollback rules:
  - if refunded invoice was `Paid` and `BalanceDue > 0`, set `Status = PartiallyPaid`
  - if `PaidAmount = 0`, set `Status = Issued`
  - `Cancelled` invoices cannot receive refunds

## API Corrections

### Invoice Create
`POST /billing/invoices`
- Creates invoice as `Draft`.
- MUST NOT generate `InvoiceNumber`.

### Invoice Update
`PUT /billing/invoices/{id}`
- Allowed only when `Status = Draft`.
- Editable fields:
  - customer reference fields
  - billing plan selection
  - invoice lines
  - tax fields
  - discount fields
  - due date
  - notes
- Rejected when status is:
  - `Issued`
  - `PartiallyPaid`
  - `Paid`
  - `Cancelled`

### Invoice Issue
`POST /billing/invoices/{id}/issue`
- Valid only when `Status = Draft`.
- Generates `InvoiceNumber`.
- Uses tenant-scoped, year-based, atomic counter.
- Freezes invoice snapshot and totals.

### Invoice Cancel
`POST /billing/invoices/{id}/cancel`
- Allowed only when:
  - `Status = Issued`
  - `PaidAmount = 0`
- Rejected otherwise.

### Payment Create
`POST /billing/invoices/{id}/payments`
- Requires `IdempotencyKey`.
- Requires `AppliedAmount > 0`.
- Requires payment currency equal invoice currency.
- Duplicate request with same `(TenantId, InvoiceId, IdempotencyKey)` returns existing `PaymentRecord`.
- Must use DB-level atomic guard preventing `BalanceDue < AppliedAmount`.

### Refund Create
`POST /billing/payments/{paymentRecordId}/refunds`
- Requires `RefundAmount > 0`.
- Requires refund currency equal payment currency.
- Rejects refund if cumulative refunds exceed payment `AppliedAmount`.

## Data Storage Updates

### Collections
Use these collection names exactly:
- `billing_plans`
- `billing_invoices`
- `billing_invoice_counters`
- `billing_payments`
- `billing_refunds`
- `billing_activity_events`

### Invoice Counter
Collection: `billing_invoice_counters`

Document shape:
```json
{
  "TenantId": "tenant-id",
  "Year": 2026,
  "CounterName": "Invoice",
  "NextValue": 42
}
```

Unique index:
```text
TenantId + Year + CounterName
```

Atomic issue operation:
```text
findOneAndUpdate(
  filter: { TenantId, Year, CounterName: "Invoice" },
  update: { $inc: { NextValue: 1 } },
  options: { upsert: true, returnDocument: Before }
)
```

Invoice number format:
```text
INV-{Year}-{SequencePadded6}
```

Example:
```text
INV-2026-000042
```

### Payment Atomic Guard
Payment creation MUST use one transaction with this guarded invoice update:

```text
updateOne(
  filter: {
    _id: InvoiceId,
    TenantId: TenantId,
    Currency: PaymentCurrency,
    Status: { $in: ["Issued", "PartiallyPaid"] },
    BalanceDue: { $gte: AppliedAmount }
  },
  update: {
    $inc: {
      PaidAmount: AppliedAmount,
      BalanceDue: -AppliedAmount
    },
    $set: {
      UpdatedAt: now
    }
  }
)
```

- If `ModifiedCount = 0`, reject payment.
- Insert `PaymentRecord` only after guarded update succeeds inside the same transaction.
- If final `BalanceDue = 0`, set invoice `Status = Paid`.
- If final `BalanceDue > 0`, set invoice `Status = PartiallyPaid`.

### Idempotency Index
Unique index:
```text
TenantId + InvoiceId + IdempotencyKey
```

### Plan Version Index
Unique index:
```text
TenantId + PlanCode + PlanVersion
```

### Refund Guard
Refund creation MUST use a guarded update on payment:

```text
updateOne(
  filter: {
    _id: PaymentRecordId,
    TenantId: TenantId,
    Currency: RefundCurrency,
    RefundedAmount: { $lte: AppliedAmount - RefundAmount }
  },
  update: {
    $inc: {
      RefundedAmount: RefundAmount
    },
    $set: {
      UpdatedAt: now
    }
  }
)
```

If `ModifiedCount = 0`, reject refund.

## Business Rules Additions

- Draft invoices MUST NOT consume invoice sequence.
- Invoice number generation occurs only during `Draft -> Issued`.
- Invoice sequence is tenant-scoped, year-based, and atomic.
- Invoice numbers are never reused after cancellation.
- Issued invoices are immutable.
- Issued invoice correction requires cancellation when unpaid, or refund/reversal flow when paid.
- All monetary values MUST be `>= 0`.
- Payment `AppliedAmount` MUST be `> 0`.
- Refund `RefundAmount` MUST be `> 0`.
- Currency MUST match across invoice, invoice lines, payments, and refunds.
- Monetary rounding uses `MidpointRounding.AwayFromZero`.
- All invoice totals, payment applications, and refunds are rounded to 2 decimal places before persistence.
- Billing plan pricing changes MUST create a new `PlanVersion`.
- Invoice MUST store billing plan snapshot fields at issue time.
- Historical invoices MUST NOT read mutable plan pricing for financial values.
- Payment creation MUST be idempotent by `(TenantId, InvoiceId, IdempotencyKey)`.
- Concurrent payment submissions MUST NOT allow `BalanceDue` to become negative.
- Activity events MUST store critical identifiers as first-class fields, not metadata.

## Frontend Surface Additions

- Shell: Platform admin.
- Route: `/Platform/Billing`.
- View scope:
  - `frontend/Diten.Web/Views/Platform/Billing/Index.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/Create.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/Edit.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/Details.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/_Form.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/_Filter.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/_DataTable.cshtml`
  - `frontend/Diten.Web/Views/Platform/Billing/_IndexL10n.cshtml`
- DataTable contract:
  - `form_field_count: 9`
  - `golden_reference: compact`
  - `api_profile: proxy-profile`
- Browser JS endpoint:
  - `/Platform/Billing/api`
- Server-side proxy target:
  - Gateway `/api/platform/billing`
- Menu:
  - `_LayoutPlatformAdmin.cshtml` Platform Administration section adds `/Platform/Billing`.
- Compact section parity:
  - `_Form.cshtml` sections: Identity, Invoice Line, Notes, Lifecycle.
  - `Details.cshtml` sections: Identity, Invoice Line, Notes, Lifecycle.
- Mutability:
  - Create/Edit pages operate only on draft invoices.
  - Issued invoice actions are issue, cancel, detail, payment, refund endpoints through proxy.
