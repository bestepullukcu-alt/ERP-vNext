# Sign-off decision text — self-registration patch 05 (MOD-0186), rebased (NOT APPROVED)

**Status: DRAFT DECISION TEXT — NOT APPROVED.** Nothing here is signed off. An agent quoting this text is not approval. The owner signs off by stating the text below in their own words.

Context: the owner signed patches 1, 2, 3, 4 and 6 and deferred patch 5 (`docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`), because the Returns sign-off (`mvp6-returns-pack-signoff-owner-decision-01.md`) moved the MOD-0186 pack to `6c8fbe28…`. This package holds the rebase. Its content equals the original patch 05 except the section number (§30 → §33) and the hunk position; see `APPLY-CHECK.txt`.

## Decision text to record

> I sign off the rebased self-registration patch `docs/roadmap/plans/mvp6-self-registration-patch05-rebase-01/05-MOD-0186-self-registration-rebased.patch` (SHA-256 `7adf06ad844c85345895c95a2a0a418344b53b2dea61517f2343914ad4e9c6fd`) for `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`. It is applied only if the pack's SHA-256 before it is `6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0` and after it is `a762305ec789f0b2547d5205135a0ff70b64e83f4a63b839a985432a46d8a552`. On any mismatch the patch is rebased again and brought back to me; it is not applied by hand.
>
> It adds §33 (self-registration specification for Reverse Logistics) and changes pack text only. It authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash by an agent. Code remains with the single CT-appointed integration owner after an integrated target (Q14/Q15) exists, shipping the provider with the Returns UI and nav keys (D4). The open gaps recorded in the section stay open.

## Options

| Option | Effect |
|---|---|
| **A — Sign off (recommended)** | Returns pack becomes `a762305e…` with §33; all six self-registration sections are then in place |
| B — Hold | Patch 05 stays unapplied; Returns has no self-registration section until later |
