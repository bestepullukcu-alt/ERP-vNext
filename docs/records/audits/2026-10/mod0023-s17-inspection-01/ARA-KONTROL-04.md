# MOD-0023 §17 rev 3 — ara kontrol 04 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `d6671221…` (commit 9a743bf96). Girdi: `.ct-teslim/MOD-0023-review-rev3-r5/` (122 dosya, ZIP sha256
  `c7219256…`). Taze bağlamlı Explore alt ajanı, model sonnet, Codex tur 2 metni; yalnız klasörü okudu. reviews.tsv'ye satır yok.
- Sonuç: **KOŞULLU** — engel yok; önceki resmî turun F-1, F-2, F-3'ü kapandı; AC-W3/W17/W18 listeleri eksiksiz bulundu.
- CT doğruladı (F-1): `WorkflowTerminalTransitionWriter.cs:357-390` — başka aktörün ikizinde `MyCommittedLogAsync` boş, sürüm
  kımıldamaz, altı okumadan sonra `TransactionUnavailable` (503), 409 değil.

## Bulgular — hepsi kabul

- F-1 ORTA: O-S17-3 (i-b) gerekçesinde "yeniden okuma 409 verir" yanlış; 503 `WORKFLOW_TRANSACTION_UNAVAILABLE`.
- F-2 DÜŞÜK: AC-W8 O-S17-3'e bağlı mı değil mi tek yönde yazılmalı (madde 9 ve (i) "unchanged", kapanış ve bitiş "bağlı").
- F-3 DÜŞÜK: (a) durum paragrafındaki rev 2 onay atfı girdide doğrulanamıyor (approvals.tsv yok; kart "önceki onaylı sürüm yok"
  diyor, #241 yüzünden) — CT ilgili satırı girdiye ekler, paket cümlesi bunu belirtmeli; (b) "the review's CT note → item 2"
  atfı yanlış kaynak: içerik ARA-KONTROL-02 F-8 (ve CT'nin INSPECTION / REVIEW-REQUEST-rev3-01 notu).
- F-4 DÜŞÜK: kişi alanı adayları: serbest metin `ReasonCode` (WorkflowTransitionLog.cs:21, ApprovalTask.cs:21, 51; SamePayload
  karşılaştırır); Guid biçim kısıtı Timesheet için de (TimesheetApprovalService.cs:202, 297).
