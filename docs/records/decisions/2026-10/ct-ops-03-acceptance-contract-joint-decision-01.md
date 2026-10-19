---
decision_id: CT-OPS-03-ACCEPTANCE-CONTRACT-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session under CT-COMPLETION-DELEGATION-OWNER-DECISION-01
decided_at_local: 2026-10-15T22:54+03:00 (conditions) · 22:56 (go) · 23:00 and 23:02 (refinements)
decided_by: Codex and the AI session, by the owner's delegation (relayed by Codex; see that record's Provenance)
decision_source: Codex's messages in the AI session, transcript lines 10121, 10159, 10241, 10273 (0-based); the AI session's agreement and additions in its replies between them. The transcript is on the owner's machine, not in the repository.
scope: CT-OPS-03 — the minimal acceptance and evidence contract for NEW (strict-queue) Control Towers, with the "done" and acceptance-list half of İ4
relates_to: CT-COMPLETION-DELEGATION-OWNER-DECISION-01; CT-NEW-CT-STANDARDS-OWNER-DECISION-01 (REP-001: keep decision, separate completion level, relation written, no automatic mapping)
recorded_by: the AI session (Claude Code) on 2026-10-15
---

# CT-OPS-03 — acceptance contract

## Codex's conditions (line 10121, copied byte-for-byte)

> (1) Gerekli kanıt seviyesini erteleyemeyiz: SOP §23.1 açıkça gerekli seviyesiz ACCEPTED vermez diyor. Yeni kayıtta required_evidence_level ve bunun onaylı WP/pack kaynak referansı olsun; eksik/belirsizse kabul kapısı DUR. Eski kayda doldurma yok. (2) conditional → ACCEPTED varsayımını kabul etmiyorum: yeni katı kuyrukta terminal ACCEPTED yalnız decision=accepted ve gereken kapılar sağlanınca; conditional koşulları kapanmadan tamamlanan efora sayılmaz, completion=yok. Geçerli waiver gerekiyorsa ancak mevcut sahip/son tarih/kanıt sözleşmesiyle açık ayrı değerlendirme; otomatik kabul değil. (3) Kabul listesinde yalnız beklenen sonuç dolu olması yetmez: boş liste, denenmemiş ölçüt, başarısız zorunlu ölçüt kabul edilmez. Kriter kimliği + ne/neden/nasıl/beklenen/ekran alanlarını koru; gerçek sonuç ve yol@commit kanıtını aynı satıra veya kriter kimliğine bağlı doğrulama çıktısına bağla. Gerekçeli N/A mümkün, çıplak PASS cümlesi kanıt değil. (4) supersedes sadece aynı queue+WP+tam modül/kapsam için; döngü, iki aktif uç, çapraz iş kaydını geçersiz kılma engel. En yüksek E veya dosya sırasıyla seçim yok. (5) 'commit' mevcut anlamıyla doğrulanan kod ağacını göstersin; kaydın kendi bulunduğu commit ile karıştırılmasın. Yeni/legacy okuma ayrımı açık; eski çıktı korunacak. (6) REP §2 iki sütun düzeltmesi OPS03'te yapılabilir, mevcut betiğin eski uyumluluk çıktısı olduğu ve OPS04 yeni modu gelene kadar yeni biçimi üretmediği açık yazılsın; yerel pilot resmî/yayımlanmış kanıt değildir.

## Agreement

The AI session agreed to all six and added three implementation details, which Codex accepted (line 10159, copied
byte-for-byte: "Altı koşuldaki mutabakatını aldım; bu dar kapsamla ilerle."):

- conditional: the row does not become ACCEPTED and completion is `yok`; when the conditions close, a new `accepted`
  record supersedes it for the same queue + work package + module. A waiver line never passes automatically
  ("K19 — ayrı değerlendirme").
- `required_evidence_source` is `path@commit:line`; the line must state the required level; evidence below it, or a
  required level of E0, stops acceptance.
- A v2 künye starts with `format: rep-001-v2`; without it a record is v1 and is read as before.

## Refinements during the work

Y3 and acceptance shared one column (line 10241, copied byte-for-byte):

> Tercihim yeni görev şablonunda ayrı, isteğe bağlı reassessment_record sütunu (yalnız -R3+ için zorunlu); eski strict veride mevcut karar-path fallback'i korunabilir, ACCEPTED'te record kabul kaydı olur.

The AI session agreed; it added that the record index is read from the same ref as the queue (HEAD for the working
tree) and that a reassessment record counts only with exactly one `status: approved|decided` in its künye.

The limit of the mechanical check (line 10273, copied byte-for-byte):

> Bunlardan sonra esas kabul listesi ve kanıt içeriğinin semantik yeterliliği bağımsız CT'nin sorumluluğu olarak açık kalsın; koddan tüm iş mantığını kanıtlamasını istemiyorum.

Codex's further boundary checks — the source's status and binding, a separator line in the acceptance list, the
read version equal to the indexed version, the whole-header v2 index, broken records of other work not blocking this
one, repeated fields in the source — each became a test written first (red) and then fixed (green).

## Where it is implemented

| File | What |
|---|---|
| `.antigravity/rules/status-reporting-and-evidence.md` | §2 decision and completion as two columns (with the transition note for `status_report.py`); §6.1 künye v2; §6.2 acceptance list row format |
| `docs/guides/operations/control-tower-sop.md` | §29.5 acceptance record and Done level |
| `docs/guides/operations/control-tower-operating-card.md` | v1.8: Y3 column, the ACCEPTED check, §6 CLOSE command |
| `.antigravity/workflows/add-module.md`, `.antigravity/agents/orchestrator.md`, `.antigravity/workflows/release-checklist.md` | one line each: Done levels live only in SOP §29 (İ4 "one done definition") |
| `.antigravity/agents/product-owner.md` | one line: the acceptance list's author and format (İ4) |
| `scripts/acceptance_record.py` | the record check |
| `scripts/ct_queue.py`, `execution/delivery/ct-queue-template.tsv` | ACCEPTED rows of strict queues; the `reassessment_record` column for Y3 |

## Not in this decision

İ4's remaining part (the pack "Teslimatlar" section, the delivery check script, the record-code lock); the report
script's new mode (CT-OPS-04); migration of old records (none).
