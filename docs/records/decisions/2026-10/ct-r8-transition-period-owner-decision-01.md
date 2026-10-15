---
decision_id: CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01
status: approved — written locally; in force on main together with SOP v2.5 r3 (see "Entry into force")
decided_at_local: 2026-10-15T15:03+03:00 (clarification); 2026-10-15T15:30+03:00 (final approval)
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: owner messages in the Claude Code session "AI" (accdcf92), 2026-10-15. The texts were drafted by Codex and issued by the owner by pasting them. The session transcript is on the owner's machine, not in the repository.
scope: the end of the R8 transition, what counts as new work, what counts as a registered task source, and the transition status of each Control Tower
amends: CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01 — only its end condition ("R8/T4 kalıcı düzeltmesi yürürlüğe girdiğinde sapma sona ermeli"); every other condition of that record stays
resolves: CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01, open item "How the transition decision's end condition reads"
recorded_by: the AI session (Claude Code) on 2026-10-15, on the owner's instruction of 2026-10-15T15:30+03:00; this record makes no decision of its own
---

# R8 transition period — 30 days from the merge; new work needs a registered task source

Every quotation is copied byte-for-byte from the owner's messages. The earlier records named above are not edited.

## Decision

Clarification, 2026-10-15T15:03+03:00:

> 1. Capability, module veya Work Package
>    geliştirme süresine 30 günlük sınır koyma.
>
> 2. Mevcut ve onaylanmış işler, geçiş süresi
>    dolduğu için otomatik durdurulmaz.
>
> 3. R8 geçişi Control Tower'ın görev kaynağını
>    kalıcı kayıt sistemine almasıyla ilgilidir.
>
> 4. İlk 30 gün geçiş ve uyarı dönemidir.
>    Süre ilgili R8 değişikliği main'e
>    birleştiğinde başlar.
>
> 5. Bu sürenin sonunda kayıtlı görev kaynağı
>    bulunmayan Control Tower'lar raporda
>    açıkça işaretlenir.
>
> 6. Yeni iş dağıtımı için kayıtlı görev
>    kaynağı esas alınır.
>
> 7. Zorunlu operasyonel istisnalar yalnız
>    kullanıcı onayı, gerekçe, kapsam ve
>    bitiş tarihiyle tanımlanabilir.
>
> 8. Devam eden capability çalışmalarının
>    ilerleme yüzdeleri, kanıtları ve
>    Work Package kayıtları korunur.

Final approval, 2026-10-15T15:30+03:00 — new work:

> R8 değişikliğinin main'e birleşmesinden itibaren
> 30 takvim günü geçiş süresi uygulanacak.
>
> Bu sürenin bitiminden önce açıkça onaylanmış
> Work Package'lar, mevcut kapsamları içinde
> devam edebilir ve dağıtılabilir.
>
> Capability geliştirme süresine 30 günlük
> sınır konulmayacak.
>
> Ancak mevcut capability'nin altında sonradan
> oluşturulan yeni Work Package otomatik olarak
> önceden onaylanmış sayılmayacak.
>
> Geçiş süresi bittikten sonra onaylanan yeni işler
> için kayıtlı görev kaynağı veya benim açıkça
> onayladığım süreli istisna zorunlu.
>
> İstisna:
> - Control Tower kimliğini,
> - gerekçeyi,
> - kapsamı,
> - başlangıç ve bitiş tarihini
> içermeli.
>
> Aktif geliştirmeler kendiliğinden durdurulmayacak.

Registered task source:

> Kayıtlı kaynak, ana daldaki resmî görev kaynağı
> kayıt dosyasında ilgili Control Tower için
> geçerli ve doğrulanabilir bir satır bulunmasıdır.
>
> Yalnız dosya adının benzemesi veya ikinci bir
> CT-QUEUE.tsv bulunması yeterli değildir.
>
> SCE, mevcut kayıt satırı doğrulandığı için
> kayıtlı kabul edilebilir.
>
> SCE'nin durum anlamlarıyla ilgili ayrı
> onay sorunları bu kararla kapanmış sayılmayacak.

Dated records:

> Control Tower'ların geçiş durumları ve
> sahip kararları depoda doğrulanabilir olsun.
>
> Önceki onayları tahmin ederek oluşturma.
> Onayı doğrulanamayan Control Tower'ları
> "TEYİT BEKLİYOR" şeklinde göster.
>
> Geçmiş karar kayıtlarını sessizce değiştirme.
>
> Yeni karar kaydı, önceki kararı hangi
> kapsamda değiştirdiğini açıkça göstersin.

## Scope of the amendment

| Record | What changes | What stays |
|---|---|---|
| CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01 | Its end. Not "when the permanent fix takes effect" but: 30 calendar days from the merge of SOP r3 to `main`; afterwards only work packages explicitly approved before the end continue and may be dispatched | Confirmed receipt; approved work package or Module Pack; the separate transition reference; the report line; no writes to the SCE / MVP6 queue |
| CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01 | Its open item (a)/(b) is resolved by this record | Everything else |

## The rules in one place

These are written into SOP v2.5 r3 (§0.1, §17.1, §20, §20.1) and operating card v1.4.

1. **Period.** 30 calendar days, starting on the day SOP r3 is merged to `main`.
2. **During the period.** A Control Tower without a registered task source dispatches only under
   CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01, on that record's terms.
3. **After the period.**
   - Work packages explicitly approved before the end continue and may be dispatched within their approved scope.
   - A new work package under an existing capability is not approved in advance by that capability.
   - A work package approved after the end needs either a registered task source or a time-limited exception that the owner approved explicitly. The exception names the Control Tower, the reason, the scope, and a start and end date.
4. **Marking.** After the end, a Control Tower without a registered task source is marked in reports.
5. **No stops.** Active development is never stopped by these rules, and no capability gets a time limit.
6. **Registered task source.** A valid, verifiable row for that Control Tower in
   `execution/delivery/ct-queue-registry.tsv` on `main`.
   - A similar file name, or a second file named `CT-QUEUE.tsv`, is not a registration.
   - SCE / MVP6 counts as registered through its row `sce-mvp6`. Its separate state-meaning questions stay open.
7. **Records kept.** Progress percentages, evidence and work package records of ongoing capabilities are kept.

## Transition status on 2026-10-15

Later receipts are written as new dated records; this table is not edited.

| Control Tower | Transition decision | Registered task source | Evidence |
|---|---|---|---|
| SCE / MVP6 | not applicable | yes, row `sce-mvp6` (commit 849f9847a; counts once on `main`) | `execution/delivery/ct-queue-registry.tsv` |
| ControlTower (Legacy) | ALINDI, 2026-10-15 | no | Its message to the AI session: "Aldım: CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01 · ControlTower (Legacy) · 2026-10-15", with references WP-MDM-GP-BRAND-01, WP-AUTH-ADMIN-BOOTSTRAP-01, WP-PLT-MARKET-REFTENANT-01. The source is a session message, outside the repository |
| CRM | TEYİT BEKLİYOR | no | Not reachable from the AI session |
| HR | TEYİT BEKLİYOR | no | — |
| PPM | TEYİT BEKLİYOR | no | — |
| PVG | TEYİT BEKLİYOR | no | — |

## Entry into force

These rules, SOP r3 and card v1.4 take effect when they are on `main`. The period's start is the merge date of SOP r3,
and its end is that date plus 30 calendar days. Until then, SOP v2.5 r2 on `main` applies, with the transition decision
for the Control Towers that confirmed it.

## Open — NOT decisions

| Item | State on 2026-10-15 |
|---|---|
| SCE / MVP6 state meanings | Waiting for the SCE Control Tower |
| Automatic marking in reports | CT-OPS-04; until then each affected Control Tower's REP-001 report carries the line given in SOP §0.1 r3 |
| Push, PR and merge of the commit chain | Held by the owner |
