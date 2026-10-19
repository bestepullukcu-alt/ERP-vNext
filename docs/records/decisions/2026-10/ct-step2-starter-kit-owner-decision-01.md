---
decision_id: CT-STEP2-STARTER-KIT-OWNER-DECISION-01
status: approved — step 2 implementation, one architecture test run and one local commit; no push, PR or merge
decided_at_local: 2026-10-15T20:36+03:00
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: the owner's message "ok" (session "AI" accdcf92, transcript line 8070), answering the AI session's scope message (line 8066). The scope it answers also carries the conditions in three owner-issued texts drafted by Codex (lines 8006, 8039, 8057). Line numbers are 0-based. The transcript is on the owner's machine, not in the repository.
scope: step 2 of the approved order (CT-NEW-CT-STANDARDS-OWNER-DECISION-01 §2) — the new Control Tower starter kit and the narrow İ3 (duplicate-development discovery)
relates_to: CT-NEW-CT-STANDARDS-OWNER-DECISION-01; CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01; CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01
recorded_by: the AI session (Claude Code) on 2026-10-15, under this approval; this record makes no decision of its own
---

# Step 2 — new Control Tower starter kit and duplicate-development discovery

Only Control Towers opened after this decision are affected. The existing Control Towers, their approved work and the
SCE / MVP6 queue do not change.

## 1. The approval

2026-10-15T20:36+03:00 (copied byte-for-byte):

> ok

The AI session's message it answers ends with this scope (the AI session's text, copied byte-for-byte):

> **Şimdi siz:** Açık onay verirseniz başlarım. Kapsam:
> - geçiş tablosu (düzeltilmiş Y2 ve Y3 ile);
> - `.antigravity/scripts/verify_module_id.py` (adıyla);
> - bağımlılık dosyası;
> - diğer dosyalar;
> - bir mimari test koşusu ve tek yerel commit.
>
> Push, PR ve merge yok.

## 2. Conditions the approved scope carries

From the owner-issued texts drafted by Codex (copied byte-for-byte):

- The acceptance condition (line 8006):

> Kabul koşulu (öneri, henüz uygulanmış özellik değil): yeni Control Tower'larda zorunlu keşif çalıştırılmamış ya da "tamamlanamadı" dönmüşse, yeni geliştirmenin dağıtımı CT-PQG-01'de ENGELLEYİCİ sayılır. Eski CT'lerin onaylı işleri geriye dönük durdurulmaz.

- Rework rule Y2 (line 8039): **SUPERSEDED geçişi yalnız sonlanmamış işler için geçerli olmalı.**
- Rework rule Y3 (line 8039): **Kuyruk durumuna dönüştürülmeden, yeni uygulamayı engelleyen karar/engel olarak kaydedilmeli.** The source is SOP §28.2, not §19.
- When the quality gate applies (line 8039): **“yeni Control Tower’ın ilk geliştirme dağıtımından önce”**
- The report hash (line 8057):

> 1. Raporun SHA-256’sı: Dosyanın kendi hash satırını da içeren tamamının hash’i olarak tanımlanamaz. “Son hash satırı hariç, UTF-8 rapor içeriğinin SHA-256’sı” gibi kesin bir hesaplama kuralı kullanılmalı. Kaydedilen rapor bu kuralla doğrulanmalı.

- Manual checks (line 8057):

> 2. Elle kontrol, kalite kapısını ertelememeli: Control Tower hazırlık sırasında keşfi elle denetleyebilir. Ancak kabul edilen koşul gereği yeni Control Tower’ın ilk geliştirme dağıtımı CT-PQG-01 hazır olmadan yapılmamalı. Metin, elle kontrolün bunun yerine geçebileceği şeklinde okunmamalı.

## 3. Where it is implemented

| File | What |
|---|---|
| `docs/guides/operations/control-tower-operating-card.md` | v1.6, new §0 "Yeni Control Tower açılışı": the checklist, the discovery command and line format, the report hash rule, the "inceleme gerekli" rule, Y1–Y4, the CT-PQG-01 timing |
| `execution/delivery/ct-queue-template.tsv` | The task source template for a new Control Tower |
| `execution/delivery/ct-queue-states.tsv` | The `sop7` vocabulary: the 19 SOP §7 states |
| `execution/delivery/ct-sop7-transitions.tsv` | The allowed transitions, the evidence each needs, the source; `öneri` marks transitions SOP §7 does not state |
| `execution/delivery/ct-queue-registry.tsv` | A `vocabulary` column; `sce-mvp6` keeps its own vocabulary |
| `scripts/ct_queue.py` | Vocabulary from the registry; for strict queues: the `module` column, exits from terminal states across the queue's git history, `-R<n>` links, the §28.2 record for `-R3` and later |
| `.antigravity/scripts/verify_module_id.py` | New `--discover` and `--check-report` modes only; the existing modes are unchanged |
| `scripts/requirements-tools.txt` | `openpyxl==3.1.5` |

The evidence is in `docs/records/audits/2026-10/ct-step2-starter-kit-01/`.

## Open — NOT decisions

| Item | State on 2026-10-15 |
|---|---|
| CT-PQG-01 (prompt quality gate) | Not started. Until it is ready, a new Control Tower cannot dispatch its first development work (card §0) |
| Installing `scripts/requirements-tools.txt` from PyPI | NOT VERIFIED — needs a download; none was made |
| MOD-0028 pack contradiction | Not in this step; to be sent to its owner separately |
| Push, PR, merge | Not approved |
