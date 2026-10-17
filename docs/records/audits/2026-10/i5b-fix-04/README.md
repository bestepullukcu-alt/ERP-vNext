# Evidence — İ5B-FIX-04, instruction content read from the visible text (2026-10-17)

Authority: `CT-FIX-04-JOINT-DECISION-01` (joint decision of Codex and the AI session within the owner's authority;
relayed by Codex). The decision record lives on the Control Tower line:
`docs/records/decisions/2026-10/ct-fix-04-joint-decision-01.md` (branch `chore/ct/ct-fix-04`). This branch,
`chore/agents/i5b-fix-04` on `97fec6a4159c4cc558bfe6d6452e57c906848977` (İ5B-FIX-03), is local only; the AI session is
its single writer, Codex measured independently.

## What changed

The v5 quality review's finding 4 (an emptied `AGENTS-REFERENCE.md` passed `sync_adapters --check`) and Codex's
counter-examples during the work (an unclosed `<!--`, a required heading inside fenced code).

| File | Change | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `visible_text` (HTML comments removed; an unclosed `<!--` hides the rest), `level2_sections` (level-2 headings outside fenced code), `section_problems`; `required_headings` read from the visible text outside code — a heading kept only inside a comment or a code block is lost and the message says which | `1fd7227945b88f0a3fa75d6f54efb8f075ffb320f6f0efe014baf8c65b885b8c` |
| `.antigravity/adapters.json` | `.antigravity/AGENTS-REFERENCE.md` with 8 `required_sections`, each with its anchor sentences (12 in all), and a note that anchors are a minimum, not the whole text | `c30312b1828f96dc1c399ce5732e7c9af39d7f93b2c8503b91c8c77fc69d6784` |
| `scripts/tests/test_sync_adapters_budget.py` | 10 new tests (21 in all) | `a8fc5f6763564ff7c471ce74751e368642ada9655101faf29641b4872c4a4943` |

`AGENTS.md`, `CLAUDE.md`, `.antigravity/AGENTS-REFERENCE.md` and the generated adapters are unchanged.

| Required section of `AGENTS-REFERENCE.md` | Anchors (verbatim in its visible body) |
|---|---|
| 2. Proje Klasör Yapısı | ``Güncel servis ve domain listesi `AGENTS.md` §2'dedir.`` |
| 3. Port Şeması — tarihli atama notları | ``Tablo ve kural `AGENTS.md` §3'tedir; aşağıdaki notlar oradan kelimesi kelimesine taşındı.`` · `**port 5065**` |
| 5. Build ve Test Komutları | `./run_all.sh` · the AuthService `dotnet build … -c Debug` line |
| 8. Çalışma Akışı (Domain / Module Oluşturma) | `İki ana yol var.` · ``klasörü kur: `README.md` + `domain-config.md` + `module-packs/` `` |
| 11. Terim Sözlüğü | the `**Module Pack**` row's path pattern |
| 12. Adaptasyon Notu | `Layered Agent + Domain Package Model SOP v2.1` |
| Module ID Canonicalization Gate (DCP-002) | `is the canonical authority for every` · `never invent a MOD / PSS / NEW identity` |
| Yükleme mimarisi (tarihçe) | ``**Claude Code `AGENTS.md`'yi otomatik yüklemez**`` |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the new test file against the unchanged `97fec6a41` tool | with this package's manifest 14 failed subtests in 9 tests; with the base manifest the same 14 and the section-contract test errors; the 11 İ5B-FIX-03 tests green in both |
| Green | 21 / 21; `sync_adapters.py --check` "OK 9 adapter(s)" on the real files |
| Guard removal (`guard-removal.txt`, driver `drivers/harness.py.txt`) — in copies outside the repository | 5 / 5 red: headings read with comments, fences not code, anchors not checked, visible content not required, comments not removed |
| In the combined candidate (`ff219eb75`, see `../ct-fix-04/README.md` on the Control Tower line) | `sync_adapters --check` OK 9; `scripts/tests` 125 / 125; the architecture suite 137 / 144 with the 7 known |
| Real files | every required section present once, with visible content and all 12 anchors; `AGENTS.md`'s 15 required headings visible outside code |

Semantics stated: in `AGENTS.md` an unclosed `<!--` ends at the next `-->` (HTML reads it so) — at the top it hides
only what comes before the core marker; after the last closed comment it hides every heading that follows (both
tested).

## Codex, independently

On its fixed copies of the two production files (`candidate2-i5b-source`, then `candidate3-i5b-source`, SHA-256 of the
set file `58f75f674a186be89ac14f59289371e6eae591c77b9f5a66bbbbaa174d911ce3`): the reference file normal 0; empty, an
unclosed comment, headings only, bodies in comments, whitespace bodies, padding → 1 (7 / 7, results
`64dafc486f4d6e013af3f7b1b45a13091812647e3dd2d42cc6711a1ec5569ecd`); `AGENTS.md`'s real `## 12.` heading inside a
fence → 1, inside an HTML comment → 1, normal 0 (3 / 3, results
`c574cb54716db37a57182dcdc3ca1050793649bfd1063ad8e61279204ee54705`). Its own statement: a fixed counter-example set, not
a general quality score.

## Limits

- Anchors are a meaningful minimum, not a guarantee of the whole text; full protection would be hash pinning.
- Live loading was not measured again: the loaded files (`CLAUDE.md`, `AGENTS.md`, `.claude/rules/agents-core.md`)
  did not change.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
