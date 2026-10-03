# Q101 — mvp6-q101-control-audit-01 — README

WP-MVP6-AUD-101 · Prompt Q101 v1 · Lane AL-MVP6-AUD-101 (INS) · `/read-only-audit` → read-only-auditor (strict repository-read-only),
applying code-quality-agent and security-agent rule knowledge. Cowork chat lane on the Linux VM bridge (owner decision ~21:25; no Darwin gate).
The only write is this folder. No fixes, no git writes, no ledger or record edits, and no secret values.

- Repo: mounted copy of `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`.
- Start 2026-09-26T18:26:56Z (21:26:56 +03:00). End: see the no-change block below.
- Scratch: VM `/tmp/q101` only (scan scripts, a /tmp copy of `frontend/Diten.Web` + `.antigravity/scripts` with the four UI overlays applied, archive listings).

## Files

| File | Content |
|---|---|
| `AUDIT-REPORT.md` | SOP §37 verification report, summary counts, top risks, what needs the Mac |
| `INVENTORY.tsv` | 169 items: G1 5, G2 27, G3 101, G4 36 — item, group, path, sha256, provenance WP, environment, CT status, entry point, phases |
| `FINDINGS.tsv` | 16 findings with rule/SOP ref, severity, `path:line`, proposed fix, fix owner agent, needs Mac, O/M/P h |
| `README.md` | this file |
| `SHA256SUMS` | the four files above |

## Method

1. **Preflight:**
   - branch and HEAD matched;
   - no `.git/index.lock`;
   - `git diff --name-only HEAD` = 19 paths;
   - `git status --porcelain --untracked-files=all -- services tests scripts frontend gateway` = 127 lines (6 M + 121 untracked);
   - full `git status --porcelain` = 469 lines.
2. **Provenance:**
   - Every G1–G3 file was hashed.
   - G3 was compared with the accepted source manifests: A12 `SUCCESSOR-360-SOURCE-MANIFEST.tsv`, `FINAL-SOURCE-MANIFEST.tsv` (355), `FINAL-354-SOURCE-MANIFEST.tsv` and `source-final-341.tsv`.
   - Result: 92 files equal all four; Program.cs differs; 8 files (DocsPathGuardTests.cs and 7 TRX) are absent from them.
   - The reverse check was also run (accepted manifest → working tree): 92 equal, 53 differ, 215 missing.
   - Record, lane and environment were taken from DEV/VER/CT records, from TRX `computerName` and paths, and from AppleDouble entries in the archives.
3. **CT status:**
   - CT verdict records (`mvp6-ct-*`, `*ct-accept*`, `*ct-review*`, `*ct-disposition*`), `docs/records/decisions/2026-09/`, CT-QUEUE and MILESTONE-EVENTS.
   - For G4, each archive's 8-hex hash prefix and folder name were searched in those records.
4. **Rule scans (static):**
   - inline `on*=` handlers, `alert/confirm/prompt`, browser storage, `document.cookie`/`access_token`/Bearer;
   - hard-coded fallback text, hard-coded ports, secret-like literals, `?? "mongodb://`, direct service-port calls, `console.log`;
   - resx 7-language parity, key parity and English placeholders;
   - `PageDescription` and hard-coded text in `.cshtml`;
   - permission-key format vs PKS-001 and the packs;
   - tenant/LE/IsDeleted filters and permission attributes;
   - line length.
5. **Guard replay:**
   - The DocsPathGuard slashed and `Path.Combine` regexes, run over the G1–G3 code files: 0 non-Five references.
   - The MongoTestDatabaseGuard patterns, run over every `*/tests/*` `.cs` in the repo: no SupplyChain or docs/ offender.
6. **`verify_datatable_page.py`** (static, repository script) ran on the /tmp composed copy with `--area SupplyChain --reference slim --api-profile proxy` for Claims, Returns, SandopPlans and CapacityPlans.
7. **G4:** `tar -tzf` listings only. Content was not re-audited, per the prompt, except to check for the MOD-0185 manifest provider (none in 36 archives).

## ASSUMPTIONs

- **A1:** The owner decision "Cowork read-only lane, no Darwin gate (~21:25)" is taken from the Q101 dispatch; no record file was looked for.
- **A2:** Group 4 is read as all `*source*.tar.gz` / `*overlay*.tar.gz` (case-insensitive) under `docs/records/audits/2026-09/` modified since 2026-09-17: 36 archives.
  - This includes `BC-SOURCE.tar.gz` and five MOD-0192 `*SOURCE*` archives that the lower-case pattern alone would miss.
  - `*-source-manifest.tsv` and `.json` files are manifests, not archives, and are excluded.
- **A3:** The CT status of G4 comes from a text search (hash prefix or folder name) in CT/decision records. "No record found" means none was found by that search. It is not proof that no decision exists.
- **A4:** "Environment" for G3 is inferred from the Mac evidence: TRX `computerName` is a macOS host, the paths are under `/Users/…`, and dotnet/Mongo runs are recorded.
  - The host name is not repeated here.
  - For G1/G2 it comes from each README ("chat lane", "Linux VM bridge").
- **A5:** "ACCEPTED" for G3 means accepted for the **bounded** work package named in the record (E4 Carrier, bounded Loads, isolated Root R2 producer). It does not mean module or integration acceptance.
- **A6:** Pack authority outranks `.antigravity` (add-module Phase 0 hierarchy). The following are therefore treated as conformant:
  - pack-registered Tier-3 permission actions (`status.change`, `transition`);
  - the pack-defined S&OP/Capacity workspace (Details page, no list);
  - pack record-only verifier rows.
- **A7:** fr values identical to en ("Actions", "Finance") are valid French words, not placeholders.
- **A8:** Findings already known from earlier records are included with a reference and not re-derived: CT process note, Q69 secret scan, TRX exclusion.
- **A9:** Estimates are agent estimates in person-hours (8 h = 1 day), not commitments.

## Limits

- No build, test or runtime (no .NET, Mongo or browser in this lane). Every runtime claim is left to the Mac (AUDIT-REPORT §3).
- The rule scans are regex heuristics. A clean result means "no match found", not proof of absence.
- The guard replay is an approximation of the C# tests; the real guards must run on the Mac.
- Group 4 contents (and any secrets inside them) are covered at hash level only; Q69 is cited for their secret scan.

## No-change (end of run)

Checked at 2026-09-26T18:46:15Z (21:46 +03:00), before this folder was written:

| Check | Result |
|---|---|
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` — unchanged |
| `git diff --name-only HEAD` | 19 paths, identical to the preflight list |
| Untracked list (services, tests, scripts, frontend, gateway) | 127 lines, identical |
| Full `git status --porcelain` | 469 lines, identical |
| Staged | 0 |
| `git diff --check` | exit 0 |
| `.git/index.lock` | absent |

After this folder is written, the only expected new status entry is `?? docs/records/audits/2026-09/mvp6-q101-control-audit-01/`.

Agent PASS ≠ CT ACCEPTED — returning to CT.
