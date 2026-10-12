# Authority — heading-label apply (Q75, MVP6-WP-LABELS-APPLY-01)

Lane AL-MVP6-LABELS-APPLY-01 (chat lane on the linked Mac folder), the only pack writer and the only ledger writer for this step. Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Working tree only: no `git add`, no `--index`, no commit, no stash (owner decision Q03a: C — no commit for now).

- Owner decision: Q73 option A, CT conversation, in-app question tool, 2026-09-26 ~15:00 Istanbul; approvedBy `current-role-user-message-2026-09-26`.
- Record: `docs/records/decisions/2026-09/mvp6-pack-heading-labels-owner-decision-01.md`.
- Exact text source: `docs/roadmap/plans/mvp6-pack-heading-labels-01/SIGN-OFF-DECISION.md` sha256 `f177ac357cb7969992da6335813f03eb380ff5c3bdc155b76d1e527678df922c`; folder `SHA256SUMS` sha256 `39ae6f62fc7fd1d6631280d101ca1dabb4d61b1b7f234c5932150e0c13eecde8` (13/13 OK at preflight).
- Rule applied from the text: "each only if its target file has exactly the before SHA-256 and the result has exactly the after SHA-256"; "One named writer applies the patches after rechecking every hash; any mismatch stops that file's application."
- Scope: the 8 patches listed in `BASE-HASHES.tsv`, nothing else. Not authorised and not done: any other pack change, code, registry update, commit, push or stash.
- Pre-copies of the 8 targets were kept in VM `/tmp/q75/pre/` (outside the repository) for restore on mismatch; no restore was needed.
