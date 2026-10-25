---
record_id: TREASURY-TRM15-GAP-LIST-11
kind: sahip inceleme bulguları ve kapanışları (TRM-15 — MOD-0023 §17 + dört Treasury pack'inin D21 bağı) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — TRM-15 doğru ihtiyaçları kapsıyor")
reviewed_input: .ct-teslim/treasury-intake/treasury-TRM15-MOD0023-7ad8ab780.zip (sha256 5b064ebf0df8ca9e8a2c750ba80a044d5bb80524c84f2624d21b091ffd3da5a2)
fixed_in: MOD-0023 pack §17, MOD-0126 / 0119 / 0120 / 0124 pack'leri, karar kaydı D22 (bu kaydın commit'i)
decisions: D22 (Escalate v1 dışı; yarış testleri geçmezse ek bloke; D21/4 açıklaması)
previous: GAP-LIST-09-TRM14.md
---

# TRM-15 — gap listesi 11 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P1 | D21/4 (200, aynı sonuç) ile §17.3 (ara 503, 409) farklı garanti | §17.3/4 hedef yanıt tablosu: son yanıt 200 ve aynı kayıt; ara 503 son yanıt değil (tekrar); **bütünleşmiş başlatma / kaydedilmiş geçiş, örnek sonradan kapansa da 200** (ölçüldü: `ExistingStartResponseAsync`, `StartWorkflowInstanceHandler.cs:372-391`); hiç bütünleşmemiş ve kurtarmanın kapattığı başlatma 409 `WORKFLOW_INSTANCE_START_CONFLICT` (`:393-398`) — tanımlı kapanış yanıtı, iş reddi değil; farklı olgu / başlatan 409 (anahtar kötüye kullanımı). D22/3: D21/4'ün bu okuması karar kaydına açıklama olarak yazıldı — **sahibin onayı istenir**. |
| 2 | P1 | Yazma davranışı hedefleniyor, yazma yolu yasak | D22/2: AC-W5…W8 bugünkü kodla geçmezse §17 bloke kalır; düzeltme ayrı §17 revizyonu + yeni onay; Treasury yeniden yazma kapısı kapalı. §17 durum notu ve izinli yollar cümlesi. |
| 3 | P1 | AC-W6 iki farklı yarışı karıştırıyor | AC-W6 Approve–Approve, AC-W7 Reject–Reject (+ RequestInfo–RequestInfo), AC-W8 Approve–Reject çatışması: tek terminal karar, kaybedene 409 `WORKFLOW_TASK_INVALID_STATE` (ölçüldü `WorkflowTaskTransitionSupport.cs:87`), kaybedenin kendi anahtarının tekrarı aynı 409. Treasury: kaybeden niyet tx2 eşlemesiyle `Refused`. |
| 4 | P2 | Treasury'nin tüm geçişleri testte yok | §17.4a eylem matrisi: start, approve (+ ApproveWithSodWaiver), reject, request-info (MOD-0119 RequestInfo, MOD-0126 RequestEvidence) → uç + replay AC + yarış AC. **Bulgu:** MOD-0126 Escalate'in MOD-0023'te görev düzeyinde karşılığı yok (yalnız `POST escalations/run`, SLA işi) → D22/1: Escalate v1'den çıkarıldı (komut, izin, denetim olayı, uçlar; `Escalated` enum'da korunur, girişi yok). `delegate`, `cancel` Treasury'de kullanılmıyor. |
| iyileştirme | — | AC-W3 "existing consumer tests" somut değil | Ölçülen tüketiciler: Inventory `GatewayWorkflowClient` (`QuarantineWorkflowTests`, `QuarantineArchitectureTests`), CRM `GatewayClaimWorkflowClient` (`ClaimsWorkflowApprovalTests`, `KnowledgePathReviewTests`), MDM `GatewayWorkflowClient` (test **yok**), Web `ClaimsController.V2` (test adı bulunamadı), Treasury (yeni). Platform build tek başına uyumluluk kanıtı değildir; TRM-15 kapanışı tüketici sahiplerinin kanıtını ister. |

Dört Treasury pack'i: yarış maddesi §17.3/4 ve §17.4a'ya bağlandı, kontrol listesi ve dilim önkoşulları AC-W5…W8, yeniden
yazma kapısı "AC-W5…W8 geçmişse" koşulunu aldı. MOD-0126'da Escalate kaldırıldı (11 yer).

Kontroller: dört pack 24 bölüm, AC'ler tekil, dilimler okunuyor. Belge incelemesidir; build / test / runtime yok.
