# Owner decisions — MVP6 sequencing (2026-09-26, CT conversation, question tool)

Recorded by AL-MVP6-READINESS-01 at 2026-09-26T11:54:33+0300 on CT instruction (CT writes no files).

1. ~11:50 — Integration timing: **finish modules first; integration at the end** (owner chose against CT's recommended 'early, incremental integration'). Consequences stated by CT: each module is completed and runtime-verified in its own isolated environment (HEAD archive + that module's accepted overlays); shared-file changes (Program.cs, gateway, navigation, L10n) are kept as separate per-module overlay packages and reconciled once at integration; module closure (add-module Phase 4.5 in the integrated target) waits for the final integration; the self-registration D4 rule ('provider ships with the module UI in the integrated target') conflicts and needs a separate owner decision.
2. ~11:52 — 'Follow the development plan; develop in parallel whatever can run in parallel.' Pilot limit stays: ≤2 product lanes + 1 environment lane; one writer per shared seam.

Supersedes the stage order of plan v9.2 §4 for Stage 2 (integration) only; plan v9.3 to follow.
