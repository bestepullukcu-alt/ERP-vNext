# CT record — Terminal lane security 02: product-file edit block (2026-09-26)

Observed 10:27: the Terminal A12 lane is in auto mode (commands "Allowed by auto mode classifier"). Owner decision (question tool): **keep auto mode + block product edits**. approvedBy: current-role-user-message-2026-09-26.
Successor to `mvp6-ct-terminal-lane-security-2026-09-26.md` (not edited).

Added deny rules in `.claude/settings.local.json` for Edit, Write and NotebookEdit on (relative and absolute repo paths):
services/, frontend/, gateway/, execution/, .antigravity/, docs/analysis/, docs/roadmap/, docs/records/decisions/, docs/guides/, docs/reference/, tests/, scripts/, .claude/, AGENTS.md, CLAUDE.md, *.sln, Directory.*.props.
Still writable for the lane: its output folder under docs/records/audits/2026-09/ and /private/tmp. Git-write and rm -rf blocks from record 01 stay.
After: sha256 `92b4fc2a626efdc69d3bd6c92aa6247cc50adda23069264ed6b7c3df03a2d1bc`. Pre-edit copy in .git-backups/ (git-ignored).
Limit: Bash commands (e.g. sed -i, python writing files) are not covered by Edit/Write rules; the auto-mode classifier and the post-run CT check (tracked diff unchanged) cover them.
