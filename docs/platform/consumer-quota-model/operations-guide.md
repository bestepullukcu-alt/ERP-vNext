# Consumer / Quota Model Operations Guide

## Standard Quota Flow

1. Initialize tenant quotas when subscription state is assigned or changed.
2. Sync limits from subscription configuration.
3. Services consume quota through internal endpoints before performing limited work.
4. Services release quota when a reserved operation is rolled back or disabled.

## Operational Signals

- Limit exceeded responses should not increment usage.
- Internal unauthorized responses usually mean missing API key, missing tenant ID, or missing source.
- Recalculation may return not-supported for counters without a backing usage source.

## Boundaries

Gateway-level API call counting and full quota dashboards are later integration/UI work. MOD-0033 currently provides the core quota model and runtime enforcement endpoints.

