# Evidence — step 2, follow-up 01 (2026-10-15)

Owner-approved narrow follow-up to step 2, after an independent review (Codex, 2026-10-15, issued by the owner):

> Hüküm: 2. adım henüz kapanmamalı. İki hata ve bir kanıt eksikliği buldum.

The step-2 commit 543f6ba74 is not changed; this follow-up is a new commit on top of it. `README.md`, `checks.txt`,
`run.sanitized.trx` and `verify-module-id-noregression.tsv` in this folder describe 543f6ba74 and are not edited.
Details are in `checks-followup-01.txt`.

## The approval

2026-10-15T21:26+03:00 (session "AI" accdcf92, transcript line 8836, 0-based; copied byte-for-byte):

> ok

It answers the AI session's message at line 8832 (the AI session's text, copied byte-for-byte):

> **Şimdi siz:** Takip paketini onaylıyor musunuz? Kapsam: iki kod dosyası, üç yeni kanıt dosyası, bir mimari test koşusu ve tek yeni yerel commit. Push, PR ve merge yok.

The package itself is described in the AI session's message at line 8822. The ControlTower (Legacy) session gave a
single-writer agreement for `.antigravity/scripts/verify_module_id.py` before it was written; that is not an owner
approval. The operating card is not changed.

## Findings and fixes

| # | Found by | Defect in 543f6ba74 | Fix |
|---|---|---|---|
| 1 | Codex | `ct_queue.py`: for a strict queue, a missing transition table, or one without rows for the queue's vocabulary, made the profile `None`; the `module`, `-R` and history checks then switched off silently. A READY row with MOD-9999 was "DAĞITILABİLİR", exit 0 | For a strict queue the profile is mandatory. A missing, unreadable or invalid table, or no rows for the vocabulary: `validate` errors, `require` says DUR (exit 1). `check_queue` also errors when a strict queue is checked without a profile |
| 2 | Codex | `verify_module_id.py --discover`: a route without its fields (`{"Routes":[{}]}`) was skipped, giving "eşleşme bulunamadı", exit 0 | Every route must be an object with a non-empty `UpstreamPathTemplate` that yields a resource name and a non-empty `DownstreamHostAndPorts` list whose entries carry a `Port`; otherwise "tamamlanamadı", exit 2, naming the route. All ports of a route are counted, not only the first |
| 3 | Codex | The report gave the number of inspected refs and a digest of their list, not the refs and their commits | Report v2 lists every inspected ref with its commit SHA (`ref: <sha> <ref>`), after the gaps and before the result |
| 4 | Codex | Module packs are matched by file name; the report did not say so | New `EKSİK` line: pack contents are not read; matching is by file name (id and slug). Also in the script's header comment |
| 5 | AI session | A route that is not an object, or whose host list is not a list, crashed the tool (exit 1, outside the 0/3/2 contract); so did a short row in the task-source registry | Both are "tamamlanamadı", exit 2 |
| 6 | AI session | Any other unexpected error crashed the tool with exit 1 | Caught as "tamamlanamadı — beklenmeyen hata (<type>)", exit 2 |
| 7 | AI session | The agreed scope named git blob ids for the sources; the report gave sha256 only | Blueprint, module registry, gateway and task sources carry both sha256 and the git blob id |
| 8 | AI session | A task source without an `item` or `module` column, or with a short row, matched nothing and said nothing | "tamamlanamadı", exit 2: a task source needs `id`, `state` and `item` or `module`, and every row the header's width |
| 9 | AI session | A partial Blueprint row (some fields filled, id or name missing) or a module registry that yields no rows was not reported | "tamamlanamadı", exit 2. The real workbook has 0 partial rows: its 648 rows without an id are completely empty |
| 10 | AI session | `ct_queue.py`: when the module registry yields no rows, the module check still passed ids found in the Blueprint or packs | Fail-closed, like an unreadable Blueprint |
| 11 | AI session (during the work) | A missing task-source registry was not mentioned in the report | A `kaynak görev-kaynakları` line says it is missing, or how many sources were read |

Examined and **not** a defect: in the queue history check, a version whose row lost its state column is skipped, but the
comparison bridges over that version, so an exit from a terminal state is still caught (ACCEPTED → short row → READY
is reported). A self-test case now proves it. The AI session had listed it as a possible defect before measuring.

Beyond the package text, two details were added in the same files and the same class of defect: an id without a name
counts as a partial Blueprint row too (the package named only "a name without an id"), and finding 11.

## A statement corrected

In its report on 543f6ba74 the AI session wrote that on broken or missing input the tool says "tamamlanamadı" in every
case. Only six cases had been tested; the general claim was wrong, as finding 2 shows. The README of 543f6ba74 lists the
tested cases and is left unchanged.

## Proof

| Check | 543f6ba74 | Follow-up |
|---|---|---|
| Strict test queue in a scratch clone, transition table moved away: `require WP-X` (MOD-9999) | DAĞITILABİLİR, exit 0 | DUR, exit 1 |
| The same, `validate` | OK, 0 errors | FAIL, profile error |
| The same, table without `sop7` rows: `require` | DAĞITILABİLİR, exit 0 | DUR, exit 1 |
| Gateway `{"Routes":[{}]}`, a route without `Port`, a route without a resource name (scratch clone, `origin/main` moved there) | exit 0, "eşleşme bulunamadı" | exit 2, route named |
| Gateway route not an object; host list not a list; task-source registry row with 2 columns | crash, exit 1 | exit 2 |
| Task source header without `item`/`module`; a row cut to 3 columns | exit 0 | exit 2 |
| One route with two hosts | port 5057 only | 5057, 5063, "AYNI KAYNAK BİRDEN ÇOK SERVİSTE" |
| Real data: "Position & Organization Assignment" + `position-assignments` | exit 3 | exit 3, the same 12 BULGU/BİLGİ lines, plus 551 ref lines and blob ids |
| `--check-report`: valid · one ref SHA changed · hash line removed | — | 0 · 2 · 2 |
| `ct_queue.py --self-test` | 46/46 | 52/52; with the new guards removed: 49/52, the three named cases fail |
| `--check-id` / `--check-all` / `--candidate`, `origin/main`'s script against the follow-up's | — | 172 active ids, 344 comparisons, `--check-all`, 3 candidates: 0 differences; CLI: the same 5 commands identical, usage text as in 543f6ba74 |
| SCE / MVP6, 543f6ba74's `ct_queue.py` against the follow-up's | — | 54 refs, 27,028 id comparisons: 0 differences; `strict` = no, no profile |

## Run 2 — source identity

The code was pinned before the run and was unchanged after it:

| Item | Value |
|---|---|
| Base commit | 543f6ba746b1ccb5db5a52a6e9eae30b1b7e33c1 |
| Changed files | `scripts/ct_queue.py`, `.antigravity/scripts/verify_module_id.py` (nothing else) |
| `scripts/ct_queue.py` sha256 | `4a3b5afa882b5bba090fad7c453b8af3ef58305313c5d4d301dd1d17c891a6e5` |
| `.antigravity/scripts/verify_module_id.py` sha256 | `8a5e7c7d338c5d2b3599df662718c212f8389371dab4f64e4189279ecc9d8486` |
| Diff of the two files against the base, sha256 | `761de894e96fb2da1ddcd088855bb006478fb84fdb9fb454cfb6012e2db90cea` |
| Run 2 raw TRX sha256 (raw kept outside the repository) | `d64640e4fb10bcad5c3e8896814b452695aa800d7ec2d314b08cf56604fe0bed` |
| `run2.sanitized.trx` sha256 | `e4c726ce6caeb98752e7757a0e6571b4b9ddf18ac060f0cf46dfa3725184ba18` |
| Sanitisation | Same tool and rules as step 1; R1 306 · R2 145 · R3 1 · R4 1; home, user, unsanitised computerName and full host name left: 0 |

Result: 131/138; 7 BİLİNEN, exit 0, raw and sanitised. The seven failure fingerprints are identical to
`run.sanitized.trx` (543f6ba74) and to step 1's `run3.sanitized.trx`.

## Regression risk

- 🟢 SCE / MVP6: the new checks apply only to strict queues; measured above.
- 🟡 Gateway: a future route written in another valid Ocelot form (for example `ServiceName` instead of a host list)
  stops discovery with exit 2 and names the route. This is the intended fail-closed behaviour; the rule is then widened
  deliberately. Today all 323 routes on `origin/main` (3912d568b) meet the rule.

## NOT VERIFIED

- Installing `scripts/requirements-tools.txt` from PyPI — needs a download; none was made.
- Failure fingerprints in CI.
- The remote state of `main`. Only the local ref `origin/main` (3912d568b) was read; no fetch was made.
- What discovery cannot see: branches not on this machine, unpushed or uncommitted work elsewhere, unregistered task
  sources, pack contents. Exit 0 does not prove that the work is new.
