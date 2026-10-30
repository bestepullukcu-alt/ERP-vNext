# MOD-0023 §17 rev 3 — ara kontrol 01 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `0725300120ae…` (commit b8822a9cf). Yöntem: Explore alt ajanı, model sonnet, salt okunur; CT kendi sonucunu
  vermedi, ilk tur bulgularını ve kodu verdi. reviews.tsv'ye satır yazılmaz (Yönetici talimatı, sahip yokken 2026-10-29).
- CT doğruladı: RequestInfo sırası `WorkflowTaskTransitionSupport.cs:539-565`; RequestInfo görevi `WaitingEvidence` yapar (:578);
  `IsDecidable` = `IsOpen || Escalated`, `IsOpen` `WaitingEvidence`'ı içerir (:846-858) @4c08fc75a.

## Sonuç

- F-1 (önceki tur): kapandı.
- F-2 (önceki tur): kısmen. Madde 9 (Approve/Reject) kodla aynı; madde 10 AC-W7'yi bütün eylemler için Approve/Reject sırasıyla
  yazıyor. RequestInfo'nun sırası ayrı ve geç gelen özdeş RequestInfo hâlâ karar verilebilir görevle karşılaşır;
  `WORKFLOW_TASK_INVALID_STATE` beklentisi RequestInfo için doğru değil, sonuç ölçülmedi (benzersiz indeks
  `PlatformSchemaManifest.WorkflowWorkCenter.cs:220` olası).
- Diğer: §17.9 giriş cümlesi "new read members" (tekil olmalı); yeni arayüz üyesi için varsayılan gövde kararı yok
  (`ListByObjectIdsAsync` :161 örneği); Reject ve RequestInfo test yardımcılarında `tasks:` parametresi yok (testler izinli yol);
  dilim tablosu: DL-2 hücresinde §17.4 başlığı, DL-1 hücresinde §17.7 başlığı eksik; "Review round" paragrafı madde 14 ile 15
  arasında liste dışı.

## CT kararı

Hepsi kabul; paket yazarına düzeltme (tur 2 öncesi).
