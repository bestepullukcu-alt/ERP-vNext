# MOD-0029-FU36 Corporate Scope Governance Blocker Audit — 2026-07-25

## Blocker Summary

FU36C and FU36D cannot safely continue as if Corporate were an additional form value. The current Company-bound
instance, document, partition, and folder-access contracts do not provide a real Corporate target. This is a
cross-cutting MOD-0028/MOD-0029 architecture decision and is governed by draft DCP-004.

## Affected Modules

- MOD-0028 Documentation & Evidence Management;
- MOD-0028-FU05 Company Collection Instance provisioning;
- draft MOD-0028-FU06 Corporate Collection Instance Foundation;
- MOD-0029-FU36 Controlled Document Registration Orchestration;
- draft MOD-0029-FU37 Corporate/Company Registration Amendment;
- planned FU36C reverse navigation/legacy-bypass hardening and FU36D runtime smoke.

## Why FU36C Cannot Safely Continue

Reverse navigation and manual-link hardening need a stable rule for whether both sides belong to Company or
Corporate scope. Without a canonical scope owner, a real corporate instance, and scope-aware folder access, FU36C
could legitimize links whose metadata, storage partition, and authorization disagree.

## Why FU36D Is Paused

FU36D is the final runtime smoke/commit-separation audit. Running it before the predecessor contract is approved
would validate only Company behavior while implying the approved Corporate workflow is complete. It resumes after
MOD-0028-FU06 and MOD-0029-FU37 implementation plus FU36C.

## Why Nullable CompanyId Alone Is Unsafe

`CompanyId` currently participates in ownership, storage partitioning, folder authorization, and audit
attribution. Making it nullable without a required typed scope owner leaves components free to interpret null as
global, missing, default, or unauthorized. That creates collision and leakage risks and does not identify the
Corporate repository.

## Why Dummy CompanyId Is Prohibited

A synthetic legal entity falsely attributes ownership, can inherit company membership permissions, pollutes
audit/reporting, and risks sharing a company partition with Corporate content. It conceals the missing model
instead of enforcing tenant and scope isolation.

## Required Capability Pack

Canonical pack:
`execution/portfolio/delivery-capability-packs/DCP-004-corporate-collection-controlled-document-registration-scope.md`.

Members:

1. `MOD-0028-FU06` — Corporate Collection Instance Foundation.
2. `MOD-0029-FU37` — Corporate/Company Registration Amendment.

Both are `draft` and authorize no implementation.

## Required Approval Gates

- typed scope owner and storage partition format;
- Corporate governance owner semantics;
- corporate folder access matrix;
- provisioning uniqueness/idempotency;
- enum/shared-contract ownership;
- migration and compatibility;
- conditional UI field matrix and governed lookup sources;
- explicit promotion of DCP-004 and each member pack.

## Recommended Sequencing

1. Approve DCP-004 decisions.
2. Approve, implement, and reconcile MOD-0028-FU06.
3. Approve and implement MOD-0029-FU37 backend/frontend amendments.
4. Continue FU36C.
5. Execute FU36D last.

## Runtime Code Changed

No. This audit task creates governance/documentation records only. Any unrelated runtime changes already present
in the working tree predate this governance pass and are not claimed or modified by it.

