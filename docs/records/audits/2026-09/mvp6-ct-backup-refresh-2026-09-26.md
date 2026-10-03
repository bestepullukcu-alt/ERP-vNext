# CT record — backup refresh (Q52) and a timestamp note (2026-09-26)

Owner decision: question tool, 2026-09-26 09:41 (Istanbul) — "Yes, refresh now". approvedBy: current-role-user-message-2026-09-26.
Method: same as 25 Sep (GIT-001 §A, non-invasive): `git diff --binary HEAD`, untracked files (`--exclude-standard`, `.git-backups/` excluded), `git bundle --all`; `GIT_OPTIONAL_LOCKS=0`. No index, commit, stash or working-tree change.

| File (`.git-backups/`, git-ignored) | Size | sha256 |
|---|---|---|
| `ERP-vNext-recovery-20260926-0941.bundle` | 239,762,781 | see SHA256SUMS |
| `ERP-vNext-recovery-20260926-0941-working-tree.patch` | 461,326 | see SHA256SUMS |
| `ERP-vNext-recovery-20260926-0941-untracked.tar.gz` | 47,321,959 (3,652 entries) | see SHA256SUMS |

SHA256SUMS file sha256 `71b0f8702c69dbbd2893b161ff8866fb76aa059cd4bff6b41109237ea92e6615`. Verification by CT at 09:43: `sha256sum -c` 3/3 OK; `git bundle verify` — complete history. HEAD unchanged (`4a8d4d4b`).
The 25 Sep 22:10 backup is kept. Offsite copy: owner, as before.

## Timestamp note (K4: the audit record is not edited)

`mvp6-ct-audit-2026-09-26.md` states "09:45" in its header and §1; the file was written at 09:39 (file modification time). The correct time is 09:39.
