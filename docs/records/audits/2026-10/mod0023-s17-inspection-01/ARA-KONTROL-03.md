# MOD-0023 §17 rev 3 — ara kontrol 03 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `9735cc69…` (commit 40bd6f98b). Girdi: `.ct-teslim/MOD-0023-review-rev3-r4/` (115 dosya, ZIP sha256
  `3acd2f1a…`). Taze bağlamlı Explore alt ajanı, model sonnet, Codex tur 2 metni; yalnız klasörü okudu. reviews.tsv'ye satır yok.
- Sonuç: **KOŞULLU** — engel yok. Önceki turun F-1, F-2 (çekirdek) ve F-3 bulguları kapandı.
- CT doğruladı: mevcut onay-ret yarış testi `WorkflowCandidateDecisionMongoTests.Fix1.cs:356`; yazıcı yalnız
  `TerminalConflictException` ve `PlatformTransactionUnavailableException` yakalıyor (`WorkflowTerminalTransitionWriter.cs:333-340`);
  `TaskApprovalService.cs:372-378` iptal anahtarı aktörden bağımsız; MDM onay testlerinde workflow okuma istemcisine atıf yok
  (`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Approvals/*.cs`, grep boş).

## Bulgular (özet) — hepsi kabul

- F-1 ORTA: mevcut test kapsamı eksik anlatılıyor (Fix1 :356-377 onay-ret yarışı; D2Fix1 :236-260 ve D2 :292-299 ikiz tekrar;
  Fix1 :126-175 "unavailable" dalları; yardımcılar Fix1 :625-660). WP yalnız gerçekten eksik olanları istemeli.
- F-2 ORTA: AC-W18 iptal çağıranları eksik: TaskApprovalService :372-378, TaskReviewService :426-432,
  SweepWorkflowOrphansHandler :87-95, CRM WorkflowApprovalCore :229-236 + istemci :171-175, MDM :302 + :172-175,
  Inventory :157-161; AC-W17 iptaldeki 403'ü kontrol etmiyor.
- F-3 ORTA: O-S17-3 seçenek (i) RequestInfo için zamanlamadan bağımsızlık sağlamaz (geç ikiz yazmaya ulaşır; yazıcıda yinelenen
  anahtar yolu yok); (i) ya RequestInfo istisnası yazmalı ya da yazıcıyı kapsama almalı.
- F-4 DÜŞÜK: (b) tutma kalıbı: görev deposu için `HoldingTaskRepository` (:2737-2746), `ReadsFailWhenArmed` (Fix1 :656);
  oturumlu üyeler oturumsuza yönleniyor (IWorkflowRepositories.cs:326-336) — sarmalayıcı işlem dışında yazar; `Hold` yalnız
  ilk çağrıyı tutar, ikinci okuma için sayaç gerekir.
- F-5 DÜŞÜK: kişi alanlarına `WorkflowInstance.ObjectId` (ya da kişi taşımadığı gerekçesi).
- F-6 DÜŞÜK: "Review round" paragrafında önceki turun F-1 → madde 15 eşlemesi eksik; iki farklı F-1 karışıyor.
- F-7 DÜŞÜK: madde 1(c) gerçek depo listesine AC-W16.
- F-8 DÜŞÜK: E4 gerekçe alıntıları kartla birebir değil ("audit" düşmüş); MDM test içerikleri girdide yok (CT ekler).
