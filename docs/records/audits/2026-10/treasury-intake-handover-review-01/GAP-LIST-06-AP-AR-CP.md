---
record_id: TREASURY-PACKS-GAP-LIST-06
kind: sahip inceleme bulguları ve kapanışları (AP / AR / Cash Positioning odaklı; dört pack) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS")
reviewed_input: .ct-teslim/treasury-intake/treasury-AP-AR-CP-packs-4e01e9862.zip (sha256 e2fe8f2430ab14191bb8544004f73cb5edfe9b6488dc2bee44dd384da133333e)
fixed_in: MOD-0126, MOD-0119, MOD-0120, MOD-0124 pack'leri (bu kaydın commit'i)
decisions: D16 (AR elle tahsilat dahil, kurallarıyla)
previous: GAP-LIST-05-TREASURY-PACKS.md
---

# AP / AR / Cash Positioning — gap listesi 6 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| P1-a | P1 | Uzak idempotency güvencesi sınırsız anlatılıyor (indeksler `IsDeleted = false` filtreli) | Dört pack'e "Güvencenin sınırı": ölçüm — `WorkflowRepositories.cs:961-974` dışında WorkflowInstance / WorkflowTransitionLog için silme yolu yok, şemada `ExpireAfter` yok (`PlatformSchemaManifest.WorkflowWorkCenter.cs` sha256 `666b44dc…4706f10` @ `4e01e986…`). **Ek ölçüm (yazar ajan, Treasury Intake CT doğruladı):** `instances/by-objects` silinmişleri gizler (`GetWorkflowInstancesByObjectsHandler.cs:51`, `DeletedAt is null`). Kural: silinmiş durum görülürse ya da görülemiyor ve ilk deneme kayıtlıysa yeniden yazma yok, niyet `Unknown`. MOD-0023 saklama + silmeme politikası ve silinmişi ayırt eden okuma **engel** (platform-shared-services). AC (sabotajlı): MOD-0126 AC-35, AP AC-37, AR AC-37, CP AC-35. |
| P1-b | P1 | Okunan sonucun doğru denemeye ait olması | Eşleşme: kiracı + nesne + gönderim sırası (ya da karar kimliği) + deterministik uzak anahtar (+ geçişte görev + işlem); eski denemenin sonucu yeni niyeti kapatamaz; yalnız nesne kimliği / "en yeni örnek" yasak. **Ek ölçüm (doğrulandı):** okuma DTO'ları anahtar taşımıyor — `WorkflowInstanceStatusDto` (`WorkflowModels.cs:200-207`) alanları: Id, Status, Outcome, Stage, Step, Started, Completed. Bu yüzden MOD-0023'e eklemeli okuma alanları (D9) gelene kadar Reconcile `Unknown`'u okumayla kapatamaz — engel. AC (sabotajlı): MOD-0126 AC-36, AP AC-38, AR AC-38, CP AC-36. |
| P1-c | P1 | AP kredi kapasitesi formülü onayda belirsiz | Gönderim: `Credited + Reserved + thisAmount ≤ Gross`; Onay: `Credited + (Reserved − thisReservation) + thisAmount ≤ Gross`. AC-39: kalan kapasitenin tamamını kullanan dekont onaylanır, bir birim fazlası 422; sabotaj: onayda gönderim formülü kırmızı. |
| P2-a | P2 | AR mükerrer tahsilat | D16: iş tekilliği (kiracı, şirket, müşteri, normalize referans), geri alınmamış tahsilatlarda tekil kısmi indeks, `RECEIVABLE_PAYMENT_DUPLICATE` (409); yarış testi AC-39 (sabotajlı). |
| P2-b | P2 | AR yanlış elle kaydın düzeltme yolu yok | D16: onaylı geri alma (`AdjustmentType = PaymentReversal`, MOD-0023 onayı, SoD A7 — kaydı giren geri almayı onaylayamaz, K2), ekleme-yalnız karşı kayıt, fatura bakiyeleri geri yüklenir; geri alınan referans yeniden girişe açılır (yazılı politika). AC-40, AC-41. O-9 = KARAR D16. |
| P2-c | P2 | ZIP tek başına yeterli değil | ZIP'e MOD-0126 pack'i ve tüm dosyaların sha256'sını içeren `MANIFEST.sha256` eklendi; üç pack'te "Ortak tanımlar: MOD-0126 pack @bu commit" satırı. |
| iyileştirme | — | AP `Σ NetAmount = totalAmount` net mi brüt mü | Ölçüm: `invoice-match.openapi.yaml` (sha256 `41fe1f43…3d11`) `totalAmount` :190 açıklamasız, satırda vergi alanı yok (:160-168) → belirsiz; procurement sahibine soru (O-5), `AP_MATCH_AMOUNT_BASIS_UNCONFIRMED` (422) yanıt gelene kadar PO'lu faturada onayı kapatır; vergili fatura testi AC-40 zorunlu. |
| iyileştirme | — | Atıfları sabitleyin | "Recovery HEAD 2026-10-24" atıfları "Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c" oldu. |
| iyileştirme | — | AC durumları | Her pack'in AC girişine: durum (tanımlı / uygulanmış / çalıştırılmış / geçmiş) `scripts/acceptance_record.py` kabul kaydında izlenir; pack'teki her AC yalnız "tanımlı". |

**Sahibin gözden geçirmesi önerilen iki seçim (yazar ajan):** (1) P1-a/P1-b nedeniyle yeniden yazma, saklama politikası **ve**
silinmişi ayırt eden okuma gelene kadar kapalı (daha gevşek seçenek: yalnız silmeme politikası yeter); (2) tahsilat geri alma
izni ayrı değil, `receivable-adjustments.*` izinleri kullanılıyor.

Kontroller: 24 bölüm; AC kimlikleri tekil (MOD-0126 37, AP 40, AR 41, CP 36); dilim tabloları okunuyor; her AC bir dilimde
kapanıyor. Belge incelemesidir; build / test / runtime yok.

## Kapanış incelemesi (2026-10-25, sahip)

Sonuç: gap listesi 6'nın belge düzeltmeleri **PASS**; geliştirmeye hazırlık **CONDITIONAL PASS**. İnceleme girdisi
`treasury-AP-AR-CP-packs-14e650974.zip` (sha256 `1d82ae81b456cec72525c0fcebd971db6744745896140504c13824df4d2df43d`;
manifestteki 10 hash geçti). Yeni kesin belge hatası yok. Kalan iş kanıt üretmektir:

1. MOD-0023 saklama politikası, Reconcile eşleme alanları ve aynı anahtarlı yarış davranışı — üretici sahibiyle (platform-shared-services).
2. AP `totalAmount` net/brüt — procurement (MOD-0143) sahibiyle; vergili fatura testi çalıştırılacak.
3. AR geri alma senaryoları — sahip önerisiyle MOD-0120 pack'ine **AC-42** (tamamen ödenmiş faturanın yeniden açılması,
   birden fazla tahsilattan birinin geri alınması, aynı tahsilata eşzamanlı iki geri alma, uzak onay sonrası yerel commit
   hatası; sabotajlı) eklendi, DL-3'te kapanır.
4. SEC-002 kapsamı, denetim ve makbuz kurallarının gerçek Mongo işlem testleriyle kanıtı — kod yazıldığında (level 2).

`draft → ready-for-dev` açık bağımlılıklar kapanmadan yapılmaz.
