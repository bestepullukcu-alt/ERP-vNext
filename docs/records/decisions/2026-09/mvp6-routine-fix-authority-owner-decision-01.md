---
decision_id: MVP6-ROUTINE-FIX-AUTHORITY-OWNER-DECISION-01
status: approved
decided_at_local: 2026-09-25T22:17+03:00
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer in the MVP6 Control Tower conversation (in-app question), 2026-09-25
scope: new MVP6 work packages approved from 2026-09-25 onward
---

# MVP6 routine-fix authority — owner decision

Question put to the owner: whether the standing clause proposed in *MVP6 Geliştirme Önerileri Rev2* applies.
Answer selected: **"Yes, for new packages."**

## Clause (added to every new MVP6 work-package approval)

> "Within the stated owned paths and acceptance criteria, defect fixes and their regressions that create no new wire
> behaviour and no shared change are within this work package's authority."

## Limits

- Applies only to work packages approved on or after 2026-09-25. Existing approvals are not reinterpreted or widened.
- Never covers: new contract/wire behaviour, shared security, migration, scope expansion, shared surfaces
  (Auth, Gateway, navigation, localization, permissions, `Program.cs`), or any explicit exact-hash constraint.
- Each fix still needs writer evidence, independent VER on the frozen source and a CT disposition. Commit, push,
  publication, pack promotion and rollout remain separate decisions.

Process reference: `docs/guides/operations/mvp6-development-process-v1.0.md` §7.
