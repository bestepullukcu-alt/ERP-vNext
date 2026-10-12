---
decision_id: MVP6-LOADS-PUBLICATION-GUARD-01
status: approved
decided_at_local: 2026-09-26T00:37+03:00
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer "Approve as prepared" in the MVP6 Control Tower conversation (in-app question), 2026-09-26
bound_to: docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/OWNER-DECISION-TEXT.md sha256 8cd55b8200f648a114df51b4a44e1ea56f011f3c2de2d9f396d493707184990d
payload_sha256: a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0
approvedBy_value_for_W4: current-role-user-message-2026-09-26
---

# MVP6 Loads publication guard binding — owner decision (human authorization record)

This Markdown record is the human authorization the docs-organization rule requires CT to record separately. The machine-readable
decision file (`mvp6-loads-publication-guard-owner-decision-01.json`, write-set item W4) is written only by lane AL-MVP6-LOADS-PUB01
during the combined step, with `approvedBy` = `current-role-user-message-2026-09-26`.

## Decision text (verbatim, adopted as prepared)

> Under MVP6-LOADS-PUBLICATION-GUARD-01, I approve the docs-path authority payload with SHA-256 `a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0`, as prepared in `docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/` (candidate authority `docs-path-authority.candidate.json.txt` SHA-256 `36013649effa878319db150dca5a942bfac592fe012fc4c71526f0a3010ca825`). It contains exactly:
>
> 1. the canonical target `docs/analysis/contracts/shipment-bundle.openapi.yaml` re-pinned from `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` to `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`, with the carrier target and all 35 existing seals byte-identical;
> 2. eight appended seals of the Loads root-amendment evidence, at the exact paths, kinds and SHA-256 values in ANALYSIS.md §3 (3 `historical-tool`, 5 `historical-data`, empty targets), with provenance `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md` SHA-256 `84f07a6b6dde53954f2c7e2302d8a3f0a2b762b5dffd94309175a1ed89306f0a`;
> 3. no new canonical target for `loads-semantics-v3.1.0.md`.
>
> I authorize lane AL-MVP6-LOADS-PUB01, as the single writer, to do the following in one step, together with the publication already authorized by decision B (patch `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5`): write that provenance file byte-for-byte; write `docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json` as the candidate `owner-decision.candidate.json.txt` with only `status` set to `APPROVED` and `approvedBy` set to `current-role-user-message-<date of this decision, YYYY-MM-DD>`; and write `docs/reference/architecture/docs-path-authority.json` as the candidate with only `status` set to `APPROVED` and `decision` bound to that record's path and SHA-256. If the production DocsPathGuard or architecture tests fail, the same lane restores the exact pre-step bytes of every touched path and removes every file it created, so that no partial state remains.
>
> I do not authorize: any other seal or target, wildcards or folder exclusions, changes to the policy/rule, schema, reader or test, changes to historical evidence, runtime or producer uptake, UI, gateway, pack promotion, rollout, commit or push. If the payload, the provenance file, any seal input or the YAML target changes, this approval does not carry over to the new hash.

## Scope reminders

- Executed together with decision B (`mvp6-loads-root-amendment-owner-decision-b-01.md`) in one step, per
  `docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.1.md` and `docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/EXECUTION-ORDER.md`.
- The production DocsPathGuard has so far only been emulated; the real test must pass on native .NET 8 before and after the write
  (rehearsal first). On failure: STOP and restore, no partial state.
- Not authorized: any other seal or target, wildcards, rule/schema/reader/test changes, historical-evidence edits, producer uptake (C),
  UI, gateway, pack promotion, rollout, commit or push.
