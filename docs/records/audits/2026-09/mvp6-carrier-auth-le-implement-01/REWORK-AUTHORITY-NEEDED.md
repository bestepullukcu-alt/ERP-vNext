# Narrow successor authority needed

The approved artifact was applied exactly and exposed two blockers. A successor candidate must be prepared before implementation authority can be requested.

## Required candidate scope

1. **Platform tenant-context repair:** after successful internal-key validation, establish the route tenant as request tenant context before invoking tenant-filtered resolver/repositories. Preserve constant-time key comparison, tenant/actor isolation, correlation, and the existing global `/api/internal` behavior.
2. **MDM service authorization seam:** provide a least-privilege, server-to-server lookup-validation path that login/refresh can use without a caller bearer. Preserve MDM tenant isolation, active/non-deleted validation, permission boundaries, correlation and fail-closed dependency behavior.
3. **Auth behavior:** keep exactly-one selection, re-resolution on every issuance path, and omission for zero/multiple/invalid/unavailable results. Never accept client-selected LE authority or copy a stale claim.

The candidate must state exact added/changed paths and baseline/target hashes. In particular, item 2 adds MDM/security ownership beyond the currently approved ten-path manifest. This document is a scope request, not an approval and not an implementation patch.
