---
decision_id: CT-NEW-CT-STANDARDS-OWNER-DECISION-01
status: approved — decisions only; this record implements nothing
decided_at_local: 2026-10-14T13:26+03:00 (deferral); 2026-10-15T16:19+03:00 (four answers)
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: (1) owner message of 2026-10-14, drafted by Codex and issued by the owner (session "AI" accdcf92, transcript line 1005); (2) the owner's answers to the AI session's question tool, 2026-10-15 (transcript line 7344; the questions and option descriptions are the AI session's text, line 7343). The transcript is on the owner's machine, not in the repository.
scope: standards for NEW Control Towers (the per-module Control Towers after the closure), the order of the remaining Control Tower work, and the deferred improvement items İ1-B, İ2, İ3, İ4
relates_to: CT-OPERATING-MODEL-OWNER-DECISIONS-01; CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01; CT-AUDIT-CORRECTIONS-2026-10-15-01
recorded_by: the AI session (Claude Code) on 2026-10-15, under the owner's approval of step 1; this record makes no decision of its own
---

# New Control Tower standards, work order and deferred items

The goal behind these decisions, in the owner's words to the AI session on 2026-10-15: prevent errors in new Control
Towers rather than repair the old ones. The SCE / MVP6 queue and the existing Control Towers stay under the R8
transition rules.

## 1. Deferred improvement items

2026-10-14T13:26+03:00 (copied byte-for-byte):

> İ1-B, İ2, İ3, İ4 ve İ5 daha sonra ele alınacak.

İ5 has since been delivered (PR #145; İ5-B on a local branch). **İ1-B, İ2, İ3 and İ4 are deferred, not cancelled.**
Their scope was proposed in the AI session's report of 2026-10-14. The summary below is the AI session's, not the
owner's words, and it must be re-measured when an item starts:

| Item | Proposed scope (AI session, 2026-10-14) |
|---|---|
| İ1-B | The non-critical rest of İ1's instruction fixes |
| İ2 | Limit rework loops: `/add-module` says "başa dön" without a limit; the audit-trail test and the browser check run in four flows; the clean-code skill's scripts belong to other stacks; a hook ignores the validator's exit code |
| İ3 | Duplicate-development discovery before a new Capability Block or module: `verify_module_id.py` reads 2 of the Blueprint's 33 columns and not `SoR_Map`, needs `openpyxl` (missing here, so it always reports "blocked") and does not see other branches; Position Assignment was built twice; one gateway resource routes to two services |
| İ4 | One "done" definition (four exist), a "Teslimatlar" section in the pack template, an acceptance-list row format, the record-code lock |

## 2. Four answers

2026-10-15T16:19+03:00, the question tool's result (copied byte-for-byte):

> Your questions have been answered: "Yeni Control Tower'lar görev durumları için hangi seti kullansın?"="SOP §7 yaşam döngüsü (Önerilen)", "REP-001 kabul künyesi nasıl olsun?"="decision korunsun + ayrı seviye (Önerilen)", "Yeni modüllerde efor tablosu (yüzde hesabı için) zorunlu mu?"="İsteğe bağlı (Önerilen)", "Birleşik sırayı onaylıyor musunuz?"="Evet (Önerilen)". You can now continue with these answers in mind.

The options the owner selected, with the description shown to the owner (the AI session's text):

| Question | Selected option | Description shown |
|---|---|---|
| Yeni Control Tower'lar görev durumları için hangi seti kullansın? | SOP §7 yaşam döngüsü (Önerilen) | SOP'deki resmî adlar birebir (READY, DISPATCHED, IN_PROGRESS, … ACCEPTED + 6 istisna). Yalnız READY dağıtılabilir; geçişler kanıtla ilerler. MVP6'nın 7 durumu yalnız SCE'de kalır. |
| REP-001 kabul künyesi nasıl olsun? | decision korunsun + ayrı seviye (Önerilen) | Mevcut decision (accepted/rejected/conditional) anlamı korunur; tamamlanma seviyesi (WP/Module/Capability/Release Done) ayrı alan olur; CT statüsüyle ilişkisi yazılır; otomatik çeviri yok. .antigravity değişikliği olduğu için uygulama ayrıca onay ister. |
| Yeni modüllerde efor tablosu (yüzde hesabı için) zorunlu mu? | İsteğe bağlı (Önerilen) | Açılışı ve dağıtımı engellemez; şablon hazır durur. Tablo yoksa rapor 'Taban yok — hesaplanamadı' der. Yüzde yalnız geçerli taban + kabul kanıtıyla. |
| Birleşik sırayı onaylıyor musunuz? | Evet (Önerilen) | 1 birleşme öncesi düzeltmeler → 2 açılış paketi + dar İ3 + İ2–İ4 erteleme kaydı → 3 birleştirme → 4 CT-PQG-01 → 5 CT-OPS-03 asgari sözleşme → yeni CT yürürlük kapısı → 6 CT-OPS-04 → 7 İ2, İ1-B, İ4 kalanı. Her adım ayrıca uygulama onayı ister. |

## 3. What follows from them (no new decision)

- **State set.** New Control Towers use the SOP §7 lifecycle names exactly. The allowed transitions, the optional
  ("gerekiyorsa") stages, the exception states and the evidence each transition needs are defined from SOP §7.1 in
  step 2 (new Control Tower starter kit), not here. REWORK proceeds as a new version (SOP §19.2), not by moving the
  same work back.
- **REP-001.** Changing `.antigravity/rules/status-reporting-and-evidence.md` needs its own explicit approval.
- **İ4 is split.** The "done" definition and the acceptance-list row format go with CT-OPS-03 (step 5). The rest — the
  pack "Teslimatlar" section, the delivery check script, the record-code lock — is step 7 ("İ4 kalanı" in the approved
  order). A later note to Codex left out "kalanı"; this record is the reference.
- **New Control Tower go-live gate.** In the approved order: CT-PQG-01 before a new Control Tower's first dispatch, and
  CT-OPS-03's minimal acceptance contract before its first acceptance.

## Open — NOT decisions

| Item | State on 2026-10-15 |
|---|---|
| `review_by` policy for registered known failures | No owner decision; `tests/known-failures.tsv` says "karar bekliyor" |
| Failure fingerprints in CI | Equal fingerprints between a local run and CI are NOT VERIFIED |
