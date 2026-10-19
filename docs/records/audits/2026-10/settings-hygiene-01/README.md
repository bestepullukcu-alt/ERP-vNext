# Evidence — SETTINGS-HYGIENE-01, the project's pre-approved permissions narrowed (2026-10-18)

Authority: `SETTINGS-HYGIENE-01-JOINT-DECISION-01` (`docs/records/decisions/2026-10/settings-hygiene-01-joint-decision-01.md`).
Branch `chore/ct/settings-hygiene-01` on `67573a31699f5e74a9cd98f1d0830d57f4cf5a5c` (CT-FIX-07), local only. Written and
checked by the AI session; the proposal was read in full by Codex before it was applied.

| File | SHA-256 |
|---|---|
| `.claude/settings.json` before (at the parent commit) | `2f7bcc24680ab1e1f1b8d945e7729279cfb1c8721383ada04db31bc79c4f87f1` |
| `.claude/settings.json` after | `a6383fe73e6b574ea9fada76a889412d5fec95010501a39d61a0d990219a552d` |

- `checks.txt`: the structural checks (valid JSON; a strict subset in the old order; 81 unique old entries; 6 kept +
  75 removed; the hooks block equal; no extra directory and no new permission key) and the 6 kept rules with their old
  line numbers.
- `removed-entries.tsv`: the 75 removed entries by old line, SHA-256 of the entry text and category. The texts are not
  copied: many hold one user's absolute paths. Anyone with the parent commit can recompute each hash.

## Limits

- Structural checks only; no live session was run with the new file.
- The user, local and managed settings layers are outside this file and unchanged.
