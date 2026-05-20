# Tenant Module Entitlements User Manual

Open `Platform > Tenants > Details`, then select `Commercial > Module Entitlements`.

The grid shows two kinds of rows:

- `Plan` rows are read-only projections from the tenant's active subscription plan.
- Physical rows are tenant-specific `Manual Override`, `Add-on`, `Trial`, or `System` decisions.

Use `Add Module Entitlement` to grant a tenant-specific module rule. Select a module, choose the source, set enabled status, optionally set an expiry date, and save.

To block a module that comes from the plan, use the disable action on the `Plan` row. The system creates a disabled `Manual Override`; it does not delete or mutate the plan.

To restore plan behavior, remove the manual override row. The next table reload recalculates effective access from the plan and remaining physical rows.

Use Refresh Projection when you need the screen to reload the current plan-derived view. It does not store plan rows as physical entitlement records.
