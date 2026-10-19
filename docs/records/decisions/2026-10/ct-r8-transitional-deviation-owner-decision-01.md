---
decision_id: CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01
status: approved (known process deviation, transitional, per Control Tower on confirmed receipt) — ends when the permanent R8/T4 fix takes effect
decided_at_local: 2026-10-15T13:09+03:00; channel and reference format 2026-10-15T13:16+03:00
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: owner messages in the Claude Code session "AI" (accdcf92), 2026-10-15. The texts were drafted by Codex and issued by the owner by pasting them. The session transcript is on the owner's machine, not in the repository.
scope: dispatch by Control Towers other than the SCE / MVP6 Control Tower that have confirmed receipt of this decision, until the permanent R8/T4 fix takes effect
relates_to: CT-OPERATING-MODEL-OWNER-DECISIONS-01, decisions 4 and 6 (docs/records/decisions/2026-10/ct-operating-model-owner-decisions-01.md, commit 6d4a5cb7d); this record replaces neither
bound_to: docs/guides/operations/control-tower-sop.md @6b8a62d5a — §0.1 item 1 (R8), §17.1 field "CT-QUEUE Row (exact text)", §20 field "CT-QUEUE row present", §20.1, §36.2 T4
recorded_by: the AI session (Claude Code) on 2026-10-15, on the owner's instruction of 2026-10-15T13:16+03:00; this record makes no decision of its own
---

# R8 transition — known process deviation for Control Towers other than SCE

Every quotation is copied byte-for-byte from the owner's messages.

## Decision

2026-10-15T13:09+03:00:

> B seçeneğini, kontrollü geçiş olarak seçiyorum.
> SCE dışındaki Control Tower'lar mevcut onaylı Module Pack / Work Package kapsamında görev dağıtmaya devam edebilir.
> Bu durum mevcut R8/T4 kurallarıyla tam uyumlu değildir. Bu nedenle açık bir "bilinen süreç sapması / geçiş kararı" olarak kaydedilecek.
> Koşullar:
>
> * Onaysız yeni kapsam başlatılamaz.
> * Görev kimliği, sorumlusu ve onay kanıtı bulunmalı.
> * SCE kuyruğuna başka Control Tower yazamaz.
> * Sapma raporlarda görünür olmalı.
> * R8/T4 kalıcı düzeltmesi yürürlüğe girdiğinde sapma sona ermeli.
> * Bu karar yetkili kayıt ve onay koşulları sağlanmadan sessizce yürürlüğe sokulamaz.

Channel and scope, 2026-10-15T13:16+03:00:

> R8 için mevcut onaylı işlerin devam etmesini sağlayan kontrollü geçiş modelini onaylıyorum.
> Yürürlük kanalı olarak geçici süreyle ilgili Control Tower'lara açık bildirim yöntemini seçiyorum.
> Bu karar, bütün Control Tower'lara otomatik uygulanmış sayılmayacak.
> Yalnız kararı aldığını doğrulayan Control Tower'lar, onaylı Work Package / Module Pack kapsamındaki işleri bu geçiş altında yürütebilecek.

Transition reference, 2026-10-15T13:16+03:00:

> Önerilen kayıt biçimini şartlı kabul ediyorum:
> R8-SAPMA · Karar-ID · WP-ID · CT · onay kanıtı
> Ancak:
>
> * Bu gerçek bir CT-QUEUE satırı değildir.
> * Mevcut alanın anlamı sessizce değiştirilemez.
> * Gerekiyorsa geçiş referansı ayrı ve açık işaretlenir.
> * Geçerli WP veya Module Pack bulunmadan dağıtım yapılamaz.
> * Sapma raporda görünür olmalı.
> * R8/T4 kalıcı düzeltmesi yürürlüğe girdiğinde geçiş sona ermeli.

## What it deviates from

SOP v2.5 R8 requires the work package's row in `CT-QUEUE.tsv` before dispatch (§0.1 item 1; §20.1 stop condition
"WP row missing in CT-QUEUE (v2.5 R8)"), and the T4 ledger-writer template names that file. Under this decision a
Control Tower other than SCE that has confirmed receipt dispatches without that row, inside a valid work package or
Module Pack. The SOP text, T4 and the SCE queue do not change.

## How a Control Tower applies it

1. **Before the first dispatch under this decision:** it confirms receipt to the owner. Until then the decision does not
   apply to it.
2. **Dispatch record:** the fields of SOP §17.1 and §20 keep their meaning. `CT-QUEUE Row (exact text)` stays empty
   and `CT-QUEUE row present` reads `no`, because there is no row. A separate, explicitly labelled line is added:

   ```text
   R8 geçiş referansı (CT-QUEUE satırı değildir): R8-SAPMA · CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01 · <WP-ID> · <CT> · <onay kanıtı>
   ```

   `<onay kanıtı>` names where the work package or Module Pack approval is recorded (path@commit or the owner's message).
   No valid work package or Module Pack → no dispatch.
3. **Reports:** every REP-001 status report of that Control Tower carries the line
   `Süreç sapması: R8 geçişi (CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01)` until CT-OPS-04 generates it.
4. **Never** writes the SCE queue (CT-OPERATING-MODEL-OWNER-DECISIONS-01, decision 4, item 2).

## Relation to earlier decisions

- CT-OPERATING-MODEL-OWNER-DECISIONS-01, decision 6 ("B: kendi kuyruğunu kaydettiren dağıtır"): stays the target
  model and is still not effective. The "B" of this record is the transitional arrangement; the two only share a letter.
- The same record, decision 4, item 6 ("Kayıtlı görev kaynağı olmayan Control Tower'ın yeni dağıtımı kendiliğinden
  serbest bırakılmasın"): this record is the explicit, recorded release it asks for. It is not automatic: it covers
  only Control Towers that confirmed receipt, and only approved scope.

## End

When the permanent R8/T4 fix takes effect, its record supersedes this one and this record is marked `Superseded`
(`.antigravity/rules/docs-organization.md` §3).

## Open — NOT decisions

| Item | State on 2026-10-15 |
|---|---|
| Which Control Towers have received and confirmed the decision | Not tracked in this record (a dated record is not edited). Every Control Tower is NOT CONFIRMED until it confirms. Where the tracking lives in the repository is not decided |
| Permanent R8/T4 fix | Tracked separately, before CT-PQG-01; not started; SCE-specific historical records stay out of its scope |
