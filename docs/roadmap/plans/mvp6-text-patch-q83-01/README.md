# Q83 — draft text-correction patches P1…P5 (NOT applied)

Prepared 2026-09-26 18:19–18:25 +03:00 by the Q83 chat lane (single ledger writer + pack-patch author) on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. **Draft only — no pack, DCP, contract or record was edited.** Sign-off: `SIGN-OFF.md` (Q85); apply: Q86.

| Patch | Target | Preimage | Patch SHA-256 | Postimage (alone) | +/− | `git apply --check` on /tmp copy |
|---|---|---|---|---|---|---|
| P1 | `MOD-0190-sop-workflow-signoffs.md` | `2bdd533f…` | `e475c74a4c51343dc94c0034abe1851c460f5b992f8e57a11a6ad65fcfa6fd8b` | `44a377c2c9c903b8647a9fc479122ffcbd997f95eb514b804a2ec3bf8f639ac3` | +1/−1 | exit 0; reverse check OK |
| P2 | `MOD-0192-capacity-planning.md` | `7c3678bc…` | `2f0d2d0f8d7ba893cc30a7f22a44094fdff8a16a6a74bfa05c960cd08747d4ec` | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` | +1/−1 | exit 0; reverse check OK |
| P3 | `DCP-009-supply-chain-inventory.md` | `6b12ce68…` | `7bda0123d8190237b2ce69397b0b974dd904ee5516654f34cd89e78a7b4c34c0` | `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf` | +2/−2 | exit 0; reverse check OK |
| P4 | `MOD-0190-sop-workflow-signoffs.md` | `2bdd533f…` | `f0c4662d68cfc7df4616a9b2b3d637ffb80cace60071e986aaf283a64b1b2eb3` | `0f0649112899a9f1fa4f58d523bcecf1bc2fd9df3cdbef6d3fd77359120b2766` | +8/−8 | exit 0; reverse check OK |
| P5 | `MOD-0187-claims-management.md` | `8ed42fad…` | `c82deca593d9cfd0b9410b19d87607a42ebc5dda0a6474742e51fb0fd8853bc5` | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` | +10/−3 | exit 0; reverse check OK |

MOD-0190 after P1 + P4 (either order): `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913`. `verify_module_id.py`: MOD-0187, MOD-0190, MOD-0192 exit 0 (names from frontmatter).

Contents: `patches/` (5), `COMPARE-0190-PIN.md` (verdict IDENTICAL), `evidence/annex-v2.0.0-vs-v3.0.0.diff`, `SIGN-OFF.md`, `SHA256SUMS`.

## Findings

- **F-Q83-1 — `MANIFESTS.md` not patched.** The manifest list referenced from DCP-009 §21 is `docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md`. It is not DCP-owned text: it belongs to the self-registration design package whose `SHA256SUMS` (`c2fa03f6359624a68eea80a8e5d10fa9f078913880941d0790c96c755d61e356`) is bound by the approved record `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md`. Editing it would break that approval binding. Its line 141 ("MOD-0190 … and MOD-0192 …: `shell: none`, `draft`, no UI scope → no manifest now") is therefore left as history; P3 states that pack §24 controls for these two modules.
- **F-Q83-2 — stale note after P3.** MOD-0190 §24 and MOD-0192 §24 each say (open item 2) that "DCP-009 §21.1 still lists … as excluded; a separate DCP patch by its owner is required". Once P3 is applied these sentences are out of date; they are not in the P1–P5 scope and are left for the next pack text revision.
- **F-Q83-3 — P1 and P4 share one file.** Both are cut against MOD-0190 `2bdd533f…`; they touch different lines and commute. The combined postimage is given in SIGN-OFF.md so either or both can be approved.

## ASSUMPTIONs

- **A1:** P1/P2 replace the "proposal until…" sentence with a pointer to the existing `Approved:` record instead of deleting it, keeping the promotion-document provenance (minimal diff, one line each).
- **A2:** No `Approved:` lines are added to any target; traceability comes from the Q85 record at the fixed path, which binds every patch by hash.
- **A3:** P4 re-pins every forward-looking 2.0.0 reference in MOD-0190 (§3, §6, §7, §16, §18, §23.3, §23.13), not only §16/§18, so the pack does not contradict itself; §21/§22 keep 2.0.0 as the historical acceptance pin.
- **A4:** The 2.0.0 YAML is compared from its published copy `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01/publication/docs/analysis/contracts/sandop-capacity.openapi.yaml` (`9543e3f2…`), because the canonical file is now 3.0.0.
- **A5:** P5 replaces "a versioned UI dispatch is released" with "only through a CT-dispatched lane"; the effort paragraph is unchanged byte-for-byte.
- **A6:** Q84 was dispatched by CT at about 18:15 to a parallel lane; its ledger row and dispatch line are written by this lane (Q84 does not touch ledgers).
