# MVP6-MOD0186-PACK-ACTIVATE-01 — SOP §22

## Verdict

**RETURNS PATCH APPLIED IN ISOLATED CHECKOUT; PACK PROMOTION HELD. CLAIMS COMPOSITION APPLIED IN REGISTERED WORKTREE.**

## Returns

- Baseline pack SHA256: `07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7`
- Approved replacement patch SHA256: `2a1843eb52b8fd0b61a553c06a75d07b32dda57bf020734d946c004b82893709`
- Isolated checkout: `/private/tmp/mvp6-mod0186-returns-activate-01`
- Target pack SHA256 after `git apply --check` and apply: `745e9cc74abf8791d5fd9f96086ae8a721bfe041b6a4168353af059c6d34fc54`
- Published YAML: `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`
- Returns annex: `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11`
- Authority: `768757bb1fb9e39a324960e25c0e3943adb4bd6231d3dd50bf098915274406fb`

DCP-002 passed previously for MOD-0186. The patch changes only pack metadata/readiness binding; no runtime or Program.cs file is included. The shared pack remains unchanged and `draft`.

Phase 1.5 remains open in pack §27/§29: repository/index/reference-snapshot decisions and separate integration-owner slot are not recorded as complete. Therefore no promotion, DEV GO or versioned dispatch was issued.

## Claims composition

Registered worktree: `/Users/natig/.codex/worktrees/claims-dev-start-01/ERP-vNext-recovery`.

- Program.cs baseline: `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`
- Approved composition target: `a28cb1ab5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a`
- Applied only the approved Program.cs hunk; target hash matches.
- API build: **0 warnings, 0 errors**, exit 0.
- No other shared file was changed by this integration action.

Claims HTTP/JWT, producer uptake and restart evidence can now be produced by the Claims DEV lane against this composed worktree. Independent VER remains required.

## Scope and remaining gates

No canonical contract/guard change, gateway change, migration, rollout, commit or push was performed. Returns and Claims remain separate owned scopes. The next action for Returns is CT disposition of the repaired patch plus explicit Phase 1.5 closure; the next action for Claims is runtime evidence on the composed worktree followed by independent VER.
