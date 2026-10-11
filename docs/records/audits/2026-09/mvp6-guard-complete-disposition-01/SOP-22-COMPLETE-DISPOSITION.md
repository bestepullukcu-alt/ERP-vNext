# SOP §22 — MVP6-GUARD-COMPLETE-DISPOSITION-01

## Verdict

**Final disposition candidate prepared; activation NO-GO.** A fresh production scan found six historical-data files plus one candidate artifact. The candidate authority seals only the six immutable historical inputs. The candidate authority JSON itself is not sealed and must remain a disposable artifact; keeping it in the repository creates new scan findings.

## Fresh exact inventory

| File | Findings | Classification | Disposition |
|---|---:|---|---|
| `docs/roadmap/plans/mod-0183-root-uptake-recovery-01/input-manifest.json` | line 3 | historical-data | exact seal candidate |
| `docs/roadmap/plans/mod-0183-root-uptake-recovery-01/baseline.json` | 38 findings, including lines 3, 307, 385, 640, 1279…13886 | historical-data | exact seal candidate |
| `docs/records/audits/2026-09/mod-0185-dev-02/changed-files.json` | 222–224 | historical-data | exact seal candidate |
| `docs/records/audits/2026-09/mod-0185-dev-03/changed-files.json` | 206–208 | historical-data | exact seal candidate |
| `docs/records/audits/2026-09/mod-0186-prep-02/input-hashes.json` | 5–21 | historical-data | exact seal candidate |
| `docs/records/audits/2026-09/mvp6-root-guard-disposition-recovery-01/authority-candidate.json` | 9 findings | candidate artifact | do not seal; keep out of repository/production checkout |

The two MOD-0183 recovery files are immutable snapshot/manifest evidence referenced by recovery planning records, not executable tools. The three DEV/PREP manifests are immutable hash inventories, also not executable tools. The recovery `authority-candidate.json` is an unapproved working artifact and is not an active authority record; sealing it would create a recursive/self-referential disposition and is rejected.

## Final candidate hashes

- Raw target/seal payload (current 17 seals + five new historical seals): `634208abae660a3a3669cc4c9a5a59105605419a2b1c71eae50c7c16eaa42202`
- UNAPPROVED decision candidate: `462f5e94ffbecce1290fa4fea7e4cd69d3aa240ed0a0aa98809401d90e4fd5ec`
- Authority candidate: `b5c7c68c84984fd7a2d36c96c003f5808fdc48f36374df1c0f429ddd73cb900c`
- Authority diff: `ab331e214df30bed3481cf94423427f35aefcdf06e59070baab99e676177d00b`

The existing 17 seals were compared byte-for-byte before constructing the candidate. No historical source file was edited.

## Guard result and separation of causes

Fresh production-mode DocsPathGuard run: **35 PASS, 1 FAIL**. The failure is the scan assertion listing the complete inventory above. It is not the `UNAPPROVED` decision assertion: the current checkout authority remains the existing approved 17-seal record, while the new candidate is disposable. The candidate artifact findings are a real repository hygiene/ownership issue separate from the six historical-data disposition.

The fixture-only patch remains disposable and was not used to change production scan behavior. No wildcard or blanket exclusion was added.

## Owner approval required

> Approve the exact six historical-data seals and raw payload hash `634208abae660a3a3669cc4c9a5a59105605419a2b1c71eae50c7c16eaa42202`, bound to decision candidate hash `462f5e94ffbecce1290fa4fea7e4cd69d3aa240ed0a0aa98809401d90e4fd5ec` and authority diff hash `ab331e214df30bed3481cf94423427f35aefcdf06e59070baab99e676177d00b`. Keep all existing 17 seals byte-identical. Remove or relocate the unapproved `authority-candidate.json` working artifact before activation; do not seal it and do not weaken the guard.

## Post-approval plan

1. Keep candidate authority/decision JSON only in disposable working storage until owner approval.
2. Remove/relocate the unapproved candidate artifact through its owning publication lane; no historical rewrite.
3. Recompute candidate source/provenance hashes and decision hash.
4. Run production reader and full DocsPathGuard in a clean checkout.
5. Apply the single authority diff only after zero failures; then repeat the full suite and negative wrong-hash/path/history/decision tests.

Canonical authority, contract, guard code, runtime, historical inputs and git were not changed.
