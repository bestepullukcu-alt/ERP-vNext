# CT record — Terminal lane security lockdown (2026-09-26)

Owner request 10:24 "keep security of computer and data"; owner decision (question tool): **lock it down**. approvedBy: current-role-user-message-2026-09-26.
Changed file: `.claude/settings.local.json` (local Claude Code permissions; not product code). Pre-edit copy: `.git-backups/settings.local.json.before-lockdown-20260926-1025` (git-ignored) sha256 `a5ae45fbb4e9b438d05f631d43cf13d2cea53bbb48da058ad83725078e76d1d5`.
After: sha256 `29cb77436b1e1ee88c0ab1663a6c9d4dfc82fbd964cda60e8ffcbd754df874aa`.

- Removed allow rule `Bash(python3 -c ' *)` (would run any Python without asking).
- Added deny rules (deny wins over allow and over the shared `git rm *` / `git mv *` allows in `.claude/settings.json`):
  git add/commit/push/stash/reset/clean/rm/mv/checkout/restore/switch/rebase/merge/branch -d|-D/tag/update-index/gc/prune;
  `rm -rf` of the repository, `~/Projects`, the home folder and dot-paths; reading `.env`, `.env.*`, `*.pfx`, `*.p12`, `~/.ssh`, `~/.aws`.
- Not blocked: `rm -rf` inside `/private/tmp` (disposable run folder), read-only git.

After the A12 run CT checks: no secret/token in the evidence folder; processes stopped and ports closed; Mongo test data removed; `/private/tmp` run folder cleaned; tracked diff unchanged except this settings file.
