# Q162 — SOP v2.5 patch r2 (revises Q141): change notes

**Agent verdict: PATCH r2 PREPARED — not applied.** The SOP, `.antigravity`, the Q141 folder and the ledgers are unchanged.
Agent PASS ≠ CT ACCEPTED.

## 1. Metadata (SOP §17.1)

| Field | Value |
|---|---|
| Work Package ID / Prompt | Q162 — SOP v2.5 patch r2 (revises Q141) |
| CT-QUEUE row | line 211: `Q162 · SOP v2.5 patch r2 (F-Q142-1, F-Q142-2) · READY · LANE 2 (Q141 writer chat; documentation-writer) · Q161 · 2026-09-28` |
| Agent Lane ID / Type | Cowork LANE 2 (the Q141 writer chat) / DEV (patch only); placement gate `uname -s` = Linux |
| Target Agent / Entry Point | documentation-writer + @orchestrator |
| Target Branch / Expected Base HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Preflight | 2026-09-28T07:55:37Z; no `.git/index.lock`; `git status --porcelain` only (no `git diff`): 29 ` M` + 502 `??` (incl. the 2 UC-01 files and record folders) — known baseline |
| Ledgers read (not written) | CT-QUEUE `34442f7948949bec6ac5cf59a9d9da33782523d742a867fb8ea6efb2034c47f5` · MILESTONE-EVENTS `5de27e695748497da4e9da91e9cfcde4d40317e00fd9d9fcb419bec826c6858e` |
| Preimage | `docs/guides/operations/control-tower-sop.md` v2.4 `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36` (2,073 lines) — same as Q141 |
| Q141 postimage (compared against) | `814dc29d06df046fbc0532a19003e064932b2e4cf50443e7cadf674a6da3246c` (2,322 lines); Q141 `SHA256SUMS` `914dcaeb…` 3/3 OK |
| r2 postimage (on /tmp copies only) | **`c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032`** (2,327 lines) |
| Base Stack | n/a (no build) |
| Allowed Paths | `docs/records/audits/2026-09/mvp6-q162-sop-v25-r2-01/` (new) + /tmp scratch |

## 2. Sources

| Ref | Path | sha256 | Lines |
|---|---|---|---|
| V1 | `docs/records/audits/2026-09/mvp6-q142-ver-q141-01/REPORT.md` | `2267fc3be28167a89c430e5a5f6b0ed8892a0622a644c777d2ba7e87edfd98d6` | 56–60 (F-Q142-1), 61–67 (F-Q142-2) |
| V2 | `docs/records/audits/2026-09/mvp6-ct-verdicts-q154-q131-q141-q144-2026-09-27.md` | `9e1a322db2e6d8b10e22ee4bd23f3a6e45212a6166e183dcb2ad819b8fdf51e5` | 42 (OD-PLACE) |
| V3 | `docs/records/audits/2026-09/mvp6-ct-verdicts-q157-q142-q145-2026-09-28.md` | `39075081db78ba5c54a568ce9e8ebe37dcf3f5f0125cd445dc14b006c491f7a1` | 38 (CT-2: F-Q142-1/-2 → Q162), 39 (OD-Q142 "Revise before apply") |
| V4 | `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` | `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` | §1 (chain now includes Q131; the reason a fixed v1 chain goes stale) |

## 3. Every changed line vs the Q141 postimage `814dc29d…` (= `DELTA.diff`)

### 3.1 SOP (`control-tower-sop.md`; line numbers: Q141 postimage → r2 postimage)

| # | Q141 line | r2 line | Change | Finding |
|---|---|---|---|---|
| D1 | after 30 | 31–38 (+8) | §0.1: "Revision r2 (WP Q162 …)" paragraph listing F-Q142-1 and F-Q142-2 | r2 listing (Q162 NE item 3) |
| D2 | 873–875 (−3) | — | §17.1: removed "Current at v2.5: BASE-STACK v1 — … 80e31c15… (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → module draft)." The rule lines 871–872 → 878–880 stay: one versioned BASE-STACK record + SHA256 (K4) | F-Q142-2 (V1:63, :67) |
| D3 | 1992 | 1997 | T2 NEREDE: "Mac → Claude Code (local session) in the repo. Not a Cowork LANE." → "Claude app → </> Code tab → Local → ERP-vNext-recovery (Mac Claude Code). Terminal is not required." | F-Q142-1 (V1:57–58, V2:42) |
| D4 | 2007–2008 (2 lines) | 2012 (1 line) | T2 `Base Stack (required)`: the fixed v1 chain is replaced by "{BASE-STACK record path + SHA256 + layer chain as written in that record}" | F-Q142-2 (V1:64, :67) |
| D5 | 2044 (1 line) | 2048–2049 (2 lines) | T3 NEREDE, runtime branch: "Mac Claude Code for runtime VER" → "for runtime VER Claude app → </> Code tab → Local → ERP-vNext-recovery (Mac Claude Code). Terminal is not required." (static branch unchanged) | F-Q142-1 (V1:60) |

Net lines: +8 −3 +0 −1 +1 = +5 → 2,322 + 5 = 2,327.

### 3.2 `dispatch-wp.md` (Q141 `614f33f0…` → r2 `af0a080e…`)

| # | Line | Change | Finding |
|---|---|---|---|
| D6 | 41 (G2) | "T2 → Mac Claude Code (`uname -s` = Darwin)" → "T2 → Claude app → </> Code tab → Local → ERP-vNext-recovery (Mac Claude Code), Terminal not required (`uname -s` = Darwin)" | F-Q142-1 (V1:60) |
| D7 | 43 (G4) | removed " (current: BASE-STACK v1, `80e31c15…`)" | F-Q142-2 (V1:65, :67) |

No other line of `dispatch-wp.md` changed (its "PROPOSAL v1 (Q141)" status text is kept as is).

## 4. Unchanged from Q141

- All other hunks of `SOP-v2.5.patch` (R8, R9 rule text, R11 templates, §36 record fields, version cells, §0 renumbering).
- The lines v2.4 loses are still the same 7: title, `Sürüm`, `Önceki sürüm`, and the headings 0.1–0.4 renumbered to 0.2–0.5.
- The `Current at v2.5` known-baseline example in the §36.2 common rules (29 M + 2 UC-01) is not a BASE-STACK pointer and is not
  part of F-Q142-2. It stays.
- V1 O-1 (INFO, `git diff` ban) needed no action (V1:68).

## 5. Verification (this lane)

| Check | Result |
|---|---|
| `patch --dry-run -p1` of `SOP-v2.5-r2.patch` on a /tmp copy of `e85854bd…` | "checking file docs/guides/operations/control-tower-sop.md", exit 0 |
| real apply on a 2nd /tmp copy | exit 0; no `.orig`/`.rej`; sha256 `c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032` |
| lines removed vs v2.4 (`diff` v2.4 → r2, `<` lines) | 7, the same as Q141 |
| `DELTA.diff` hunks | 6 (SOP 5: §0.1, §17.1, T2 NEREDE, T2 Base Stack, T3 NEREDE; dispatch-wp 1 hunk with G2 + G4) — only F-Q142-1/-2 lines + the §0.1 r2 listing |
