# Interface Registry Operations Guide

## Discovery Flow

1. Import a manifest document.
2. Review generated discovery batches.
3. Inspect diffs for each batch.
4. Confirm or reject batches and individual diff items.
5. Query active snapshots by interface code and version.

## Review Rules

Reject actions should include a clear review reason. Deprecation should include the target version and reason so downstream teams understand the change.

## Boundaries

The registry documents interface contracts and review state. It does not automatically change gateway routes or downstream service implementations.

