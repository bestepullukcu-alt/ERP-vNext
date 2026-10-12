# CT working mode — owner instruction 2026-09-26 11:34 (permanent)
- CT only plans, produces self-contained prompts (SOP §17: metadata + NE/NEDEN/NASIL/YAPMA/DOĞRULA) and independently verifies returned evidence (K13: agent PASS ≠ CT ACCEPTED). CT runs no build/test/service and writes no files.
- Prompts are executed by the owner in a separate agent conversation; the agent cannot ask questions: resolve from contract + reports + SOP, write ASSUMPTION, continue; hard-stop only on contract break, data loss, or shared-seam edit.
- CT verdicts are written to records and ledgers by the next prompt's first step (owner decision, question tool, 2026-09-26).
- Commits: LOCAL only, no push; one push at the end of MVP6. Owner decision (question tool): allow `git add <paths>` and `git commit`; push, stash, reset, rebase, clean, rm, mv, checkout, restore stay blocked.
- Git commits run only from Mac Terminal sessions: chat lanes reach the repository through a bridge that cannot delete files, so git cannot remove its lock file there. Chat-lane work stays uncommitted until a Mac session commits it.
- Runtime work (.NET, MongoDB, browser) runs only in Mac Terminal sessions; each prompt states where it runs.
