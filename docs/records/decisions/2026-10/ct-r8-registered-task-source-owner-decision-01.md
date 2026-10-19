---
decision_id: CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01
status: approved — written locally as SOP v2.5 revision r3 and operating card v1.3; NOT in force until on main (see "Entry into force")
decided_at_local: 2026-10-15T13:52+03:00
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: owner message in the Claude Code session "AI" (accdcf92), 2026-10-15. The text was drafted by Codex and issued by the owner by pasting it. The session transcript is on the owner's machine, not in the repository.
scope: R8 in the general Control Tower SOP (everything outside §36.2) and the daily operating card
relates_to: CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01 (kept; this local commit does not supersede it); CT-OPERATING-MODEL-OWNER-DECISIONS-01, decisions 4 and 6
bound_to: docs/guides/operations/control-tower-sop.md — §0.1 item 1 and revision r3, §17.1, §20, §20.1, §36; §36.2 unchanged (section sha256 aab2542bd02cedfccbdfaf3c52de8810b9fed5f845dabfc6c5dd55b4f6a6b11d before and after); docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv unchanged (sha256 ea74b4f8a6aa601802994d8d5f6824059bba3f773e305c3dee2c5c1473118cba)
recorded_by: the AI session (Claude Code) on 2026-10-15, on the owner's instruction of 2026-10-15T13:52+03:00; this record makes no decision of its own
---

# R8 — every Control Tower uses its own registered task source

Every quotation is copied byte-for-byte from the owner's message.

## Decision

2026-10-15T13:52+03:00:

> 1. Genel Control Tower SOP'sinde R8'in tek bir SCE CT-QUEUE dosyasına bağlı olmasını düzelt.
> 2. Genel kural şu ilkeyi esas alsın:
> Her Control Tower, kendisine ait kayıtlı ve yetkilendirilmiş görev kaynağındaki doğrulanmış iş kaydını kullanır.
> 3. Kayıtlı görev kaynağı olmayan Control Tower için geçici R8 sapma kararının kapsamını ve yürürlük koşullarını koru.
> 4. SCE/MVP6'ya özel T1–T4 şablonlarını değiştirme.
> 5. SCE'nin CT-QUEUE dosyasına, tarihsel kimliklerine veya durumlarına dokunma.
> 6. Günlük kullanım kartını yeni genel kuralla uyumlu hâle getir.
> 7. Mevcut registry ve ct_queue.py mekanizmasını yeniden tasarlama.

Checks set with it:

> * Kayıtlı kuyruğu olmayan Control Tower'ın dağıtımı, yalnız açıkça geçerli ve bildirilmiş geçiş kararı kapsamında devam edebilmeli.

> * Yetkisiz veya kayıtsız yeni iş dağıtımı serbest bırakılmamalı.

> * Geçiş kararı yerel commit nedeniyle Superseded sayılmamalı.

> * Kalıcı geçişin ne zaman yürürlüğe gireceği açıkça tanımlanmalı.

## What changed

| Place | Before (v2.5 r2) | After (r3) |
|---|---|---|
| SOP §0.1 item 1 | R8 checks the row "in CT-QUEUE" | The row in the dispatching Control Tower's registered task source (`CT-QUEUE.tsv` for SCE / MVP6) |
| SOP §0.1, revision r3 (new) | — | The rule, the field names, the no-source case and entry into force |
| SOP §17.1 field | `CT-QUEUE Row (exact text)` | `Task Source Row (exact text)`, comment names the registered source |
| SOP §17.1 rule | Row copied from `CT-QUEUE.tsv`; the single ledger writer appends it | Row copied from the registered source; SCE / MVP6 keeps `CT-QUEUE.tsv` and T4. A Control Tower without a source acts only under the transition decision, field empty, separate transition line |
| SOP §20 field | `CT-QUEUE row present` | `Task source row present`; "no" stops unless the transition decision applies |
| SOP §20.1 stop | WP row missing in CT-QUEUE | WP row missing in the registered source and the transition decision does not apply |
| SOP §36 template field | `CT-QUEUE Row (exact text)` | `Task Source Row (exact text)` with the same note |
| Operating card §1 | "Ledger row (v2.5 R8)" | "Görev kaynağı satırı (R8, r3)", the same rule in short |
| Operating card, R8 note | Open architecture decision | Points to this decision and its entry into force |

## What did not change

- SOP §36.2, the SCE / MVP6 templates T1–T4: byte-identical. There `CT-QUEUE Row` keeps meaning the SCE / MVP6 source.
- `CT-QUEUE.tsv`, its historical ids and states.
- `execution/delivery/ct-queue-registry.tsv`, `execution/delivery/ct-queue-states.tsv`, `scripts/ct_queue.py`.
- `CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01`: not marked `Superseded`.

## Entry into force

1. SOP r3 and card v1.3 are in force when they are on `main`. Until then SOP v2.5 r2 on `main` applies, with the
   transition decision for the Control Towers that confirmed receipt.
2. A Control Tower uses the permanent rule once its task source is registered in
   `execution/delivery/ct-queue-registry.tsv` on `main` (a one-time registration per Control Tower).
3. When the transition decision ends is set by that decision ("R8/T4 kalıcı düzeltmesi yürürlüğe girdiğinde sapma sona
   ermeli"), not by this record. See "Open".

## Open — NOT decisions

| Item | State on 2026-10-15 |
|---|---|
| How the transition decision's end condition reads | (a) for everyone, when r3 is on `main`: a Control Tower without a registered source then stops new dispatch until it registers one; or (b) per Control Tower, when its own source is registered. The owner decides; until then the transition record stays as it is |
| Registration of each Control Tower's task source | Not started; one registry row and the Control Tower's own source file each |
| Push, PR and merge of the commit chain | Held by the owner |
