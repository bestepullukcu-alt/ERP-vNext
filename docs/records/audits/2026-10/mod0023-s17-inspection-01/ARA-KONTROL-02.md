# MOD-0023 §17 rev 3 — ara kontrol 02 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `b6971dca…` (commit 935754fa7). Girdi: `.ct-teslim/MOD-0023-review-rev3-r3/` (105 dosya, ZIP sha256
  `b15d6961…`). Yöntem: taze bağlamlı Explore alt ajanı, model sonnet (yazar opus), Codex tur 2 metni aynen; yalnız klasörü okudu.
  reviews.tsv'ye satır yazılmaz (Yönetici talimatı, 2026-10-29).
- Sonuç: UYGUN DEĞİL. CT F-1, F-2 ve F-4'ü pakette ve kodda doğruladı (AC-W2 paket:570-572 "other-tenant id → 404";
  §17.7/6 paket:684-685 INVALID_STATE "definite refusal"; ara adım `CommitStepAsync` WorkflowTaskTransitionSupport.cs:307).

## Bulgular (özet) ve CT kararı — hepsi kabul

- F-1 ENGEL: AC-W2 ve §17.3/3 hâlâ by-objects için başka kiracı → 404 diyor; by-objects 200 + boş liste döner. AC-W2 §17.9'da
  yeniden yazılmalı, bitiş listesine "§17.9'daki haliyle".
- F-2 ORTA: Approve/Reject (b) durumunda aynı anahtarlı özdeş geç tekrar 409 WORKFLOW_TASK_INVALID_STATE alır; §17.7/6 bunu
  kesin ret sayar → Treasury kayıtlı bir geçiş için `Refused` yazar. Tasarım seçimi gerekir (sahip kararı, O-S17-3).
- F-3 ORTA: (b) zorlama örüntüsü yanlış (`AllReservedBarrier` uymaz); uyan örüntü `Hold` / `HoldAfterReserveRepository` /
  kaybedeni tutan test (WorkflowStartRecoveryMongoTests.cs:2718, 2448-2463, 1870-1889); test yardımcılarında `expectedVersion` yok.
- F-4 ORTA: ara adım onayı `CommitStepAsync` ile ve iki log satırıyla yazılır; "exactly one transition row" ara adımda yanlış
  okunur; AC-W6/W8 son adım / ara adım kapsamını yazmalı.
- F-5 ORTA: tüketici listeleri eksik: Platform history testleri (WorkflowCrossServiceTests.cs:551-634), Web `getInstance` ve
  `cancelTask`, Timesheet iptali (AC-W18'in 403'ü tüketici kontrolüne bağlanmamış).
- F-6 DÜŞÜK: kişi alanları: RuntimeAssignmentSnapshot.ResolvedPrincipalId / CandidatePrincipalIds, StartRequestFingerprint,
  güvenilir iptalin ActorId Guid ayrıştırması, RequestInfo `target:` yorumu, serbest metin anahtar.
- F-7 DÜŞÜK: klasörde olmayan dayanaklar: Index.cshtml, WorkflowActorBinding.cs, PRODUCER-ANSWER-MOD0023-01.md,
  QuarantineArchitectureTests, MDM test klasörü (CT ZIP'e ekler).
- F-8 DÜŞÜK: temizlik: dilim tablosu değişikliği kaydedilmemiş; durum paragrafı "AC-W1…W14" ve "no write-path change (D22/2)"
  eski; O-S17-2 karar kaydı yok; §17.9/2 "extends" yerine "replaces" (§17.8/9 history includeDeleted iznini kaldırmalı).
