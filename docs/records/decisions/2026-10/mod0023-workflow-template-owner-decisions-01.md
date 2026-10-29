# MOD-0023 — Treasury iş akışı şablonları: sahip kararları 01

- Modül: MOD-0023 Workflow Designer (Approvals/SLAs/Escalations) — platform-shared-services
- Karar veren: depo sahibi (K1, Ali), MOD-0023 sahibi
- Kanal: CT · İş akışı sohbeti, sahibin kendi yazdığı cevap kelimesiyle
- Bağlam: MOD-0023 pack §17.8/8 — şablon kodları ve adım sayıları §17 onayından sonra MOD-0023 sahibince önerilir
  (onay: `execution/approvals/approvals.tsv`, pack sha256 803fbaa5…, origin/main f0c5014dc). Treasury paketlerinin açık
  maddeleri: MOD-0126 O-8, MOD-0119 O-11, MOD-0124 O-20, MOD-0120 AR düzeltme şablonu, MOD-0217 O-3.
- Bu kayıt kod, kuyruk satırı, push ya da PR izni vermez.

## Ölçülen dayanak (okundu, çalıştırılmadı; origin/main f0c5014dc)

- Var olan şablon örüntüsü: `services/Diten.Platform/src/Diten.Platform.API/Seed/workflow-templates/product-identity-templates.json`
  (WP-PLT-WORKFLOW-TEMPLATES-01): bir aşama, bir onay adımı, atama yok — onaylayıcıyı tüketicinin gönderdiği adaylar belirler.
- MOD-0126 onaylayıcı adaylarını kendisi çözer (SoD R1–R4; eşik üstü partide onay `finance-treasury.payment-batches.tier2-approve` ister).

## Kararlar

| # | Konu | Karar | Cevap kelimesi | Tarih |
|---|---|---|---|---|
| W-1 | MOD-0126 O-8 — ödeme partisi onayı adım sayısı | **Tek adım.** Her parti tek onayla geçer; eşik üstü partide o onayı yalnız yüksek tutar yetkisi olan kişi verir (MOD-0126 R4). Çok adımlı / tutara göre kademeli onay v1'de yok. | `tek-adim` | 2026-10-27 |
| W-2 | MOD-0119 O-11 — satıcı faturası (AP) onayı adım sayısı | **Tek adım.** Her fatura tek onayla geçer; faturayı giren kişi onaylayamaz (MOD-0119 SoD R1–R3). Çok adımlı onay v1'de yok. | `ap-tek` | 2026-10-27 |
| W-3 | MOD-0124 O-20 — günlük nakit pozisyonu onayı adım sayısı | **Tek adım** (paketin önerisi). Onaylanan pozisyon o gün / para birimi için geçerli olarak yayımlanır. Çok adımlı onay v1'de yok. | `nakit-tek` | 2026-10-27 |
| W-4 | MOD-0120 — müşteri faturası (AR) düzeltme onayı (iptal, alacak silme, tahsilat geri alma) | **Üç tür için tek şablon, tek adım.** Eşik üstü düzeltmede onayı yalnız yüksek tutar yetkisi olan kişi (finans kontrolörü) verir — AR modülünün kontrolü. Tür başına ayrı şablon v1'de yok. | `ar-tek` | 2026-10-27 |
| W-5 | MOD-0217 O-3 — sözleşme sürümü onayı (paket draft / not-approved; karar paket onaylanınca kullanılır) | **Tek adım.** Onayı sözleşme onay yetkisi olan kişiler verir, sürümü hazırlayan onaylayamaz; adayları MOD-0217 seçer ve başlatmada gönderir. Ayrı hukuk inceleme adımı v1'de yok; hukuk görüşü gerekirse onaylayıcı MOD-0023 "bilgi iste" (request-info) geçişini kullanır. Dayanak: başlatma isteği tek aday listesi alır (`StartWorkflowInstanceRequest.CandidatePrincipalIds`, `WorkflowModels.cs:286-299`); adım başına ayrı aday ölçülmedi. | `szl-tek` | 2026-10-27 |
| W-6 | Şablon kodları (W-1…W-5) | **Kabul:** `FT-PAYMENT-BATCH-APPROVAL` (MOD-0126, W-1) · `FT-AP-INVOICE-APPROVAL` (MOD-0119, W-2) · `FT-CASH-POSITION-APPROVAL` (MOD-0124, W-3) · `FT-AR-ADJUSTMENT-APPROVAL` (MOD-0120, W-4) · `MDM-CONTRACT-VERSION-APPROVAL` (MOD-0217, W-5). Var olan biçimle aynı (`GLOBAL-PRODUCT-IDENTITY`, `GSKU-CORRECTION`); `services/**` içinde başka kullanım yok (ölçüldü, f0c5014dc). Her şablon bir aşama, bir onay adımı, atama yok. | `kod-evet` | 2026-10-28 |

## Açık kalanlar (bu kayıt karar vermez)

- Şablonları kuran kod işi (Platform şablon kataloğu + `--ensure-workflow-templates`) ayrı bir iş paketidir; numarası kule kuyruğu açılınca verilir.
- HTTP zaman aşımı (15 sn) tüketici (Treasury) ayarıdır; MOD-0023 şablonunun parçası değildir.
- Canlıda şablonlar kiracı başına kurulur; modül kabulünde canlı kullanım listesine girer (kart §6.1).
