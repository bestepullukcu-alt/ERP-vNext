# Evidence — step 7, the merge preflight (2026-10-16)

Authority: `CT-MERGE-PREFLIGHT-JOINT-DECISION-01` (joint decision of Codex and the AI session under
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`). A **measurement of two sources**, not a merge and not a publication:
this branch `95de60936` and the local `origin/main` `3912d568b`. The commit that adds this folder only adds records; it
is **not** the tested tree.

## What was measured

| Check | Result |
|---|---|
| The remote `main`, once (Codex, `git ls-remote`, 2026-10-16T08:23:57Z) | `3912d568b`, the same as the local `origin/main`; no fetch; this is that moment only |
| Text merge: `git merge-tree --write-tree 3912d568b 95de60936` (Codex, then the AI session) | exit 0, no conflict; tree `136e5fa1be9508a2d4ae7efb65cb7f5c9c318ca0` |
| Changes kept | the 100 files of the Control Tower chain: 98 the same blob, 2 merged (`orchestrator.md`, `add-module.md`); the `main` instruction fixes of e66404988 present (Codex, 12/12 checks) |
| Scratch clones outside the repository | A = 95de60936; B = HEAD 95de60936 with index and worktree = tree 136e5fa1 (never a merge commit); both: `origin/main` pinned to 3912d568b (a local test copy), push disabled |
| Six self-tests, A and B | ct_queue 69/69 · acceptance_record 54/54 · prompt_gate 71/71 · status_report 75/75 · hook 21/21 · known_failures 24/24 — the same case lines in both |
| The list-screen verifier: A's and B's copy on the same source tree, each tree | 304/304 runs identical (stdout, stderr, exit); 152 screens in both trees |
| Architecture suite on B (one run, the shared test lock, restore from the local package folder) | 131/138; the 7 failures are the 7 registered known failures; fingerprints as at the CT base |
| B, all tracked files | worktree = index before (setup) and after (`git diff-files --exit-code` 0); index tree 136e5fa1 before and after |
| The TRX | raw `daf2b5da…` stays outside the repository; path roots normalised (`normalize_trx_roots.py`, roots given at run time), then the step-1 sanitiser R1–R4 → `run.sanitized.trx` `179d2aa2…`: XML valid, 138 results, names, outcomes and the 7 messages as raw, 7 known failures, no user name, home, host name or path root left; Codex reproduced both hashes |

A three-character match of the short host name sits in one test's output table as a count; it is test data, not host
metadata (Codex), and is left as is.

## Files

| File | What it is |
|---|---|
| `checks.txt` | The measurements above with their outputs; local paths replaced (`__SCRATCH__`, `__TEST_REPO_A__`, `__TEST_REPO_B__`, `__HOME__`, `__TMP__`) |
| `run.sanitized.trx` | The architecture suite on the merged tree, sanitised |
| `normalize_trx_roots.py.txt` | The path-root normaliser used before the sanitiser (no machine path inside) |
| `*-codex.json` | Codex's six independent, path-free summaries, copied unchanged |

## Limits — NOT VERIFIED or not machine-checkable

- CI has not run on the merged tree; the remote `main` is known only at 08:23:57Z.
- The merge, push, PR and visibility are the owner's decision; nothing here is publication.
- The İ5-B branch (`88205c3d6`) is separate and not part of this chain.
- The commit that adds this folder is not the tested tree; its own content is records only.
