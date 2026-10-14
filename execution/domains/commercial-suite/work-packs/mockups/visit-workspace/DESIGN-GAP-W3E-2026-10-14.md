# DESIGN GAP — W3E ek kanıtlar mockup'ı ↔ arka uç (BE-a + BE-b) — 2026-10-14

> **Mockup:** `W3E Ek Kanitlar (standalone).html` (30 ekran, seçici). Çözülmüş hali `decoded-w3e/00-template.html` + `decoded-w3e/3ef90aa6-….js`. Önizleme: launch `mockups-workspace` (5098).
> **Brief:** [BRIEF-W3E-qr-antifraud](BRIEF-W3E-qr-antifraud.md).
> **Paket:** [WP-VW-W3E-qr-antifraud](../../WP-VW-W3E-qr-antifraud.md).
> **Arka uç:** test dalında BE-a `b997aeb77` + BE-b `9294c90e1` / `302182712` + birleşim `93700f673`.
>
> **Kural:** aşağıdaki kararlar mockup'ın önüne geçer. Kararda adı geçmeyen her şeyde mockup esastır: görsel düzen, metin, durum. E4'te ekranlar mockup'la yan yana karşılaştırılır.

## 1. Kapsam karşılaştırması
Brief'in ekran listesinin **tamamı** çizilmiş: Q1 a / b / c / d + geçmiş, Q2 tekli + etiket, Q3, Q4, Q5 liste + sonuç + boş, E 1–3, S 1–8, M 1–6.

## 2. Kararlar (CT, 2026-10-14)

| # | ekran | mockup | karar | paket |
|---|---|---|---|---|
| D-1 | E1 / E2 / E3 | Ayar sayfası **katmanlı** çizilmiş: iş birimi › tüzel kişilik › kiracı, "Kapsamlı ayarlar" listesi | Katmanlı ayar henüz yok (W3-BE-d / WEB-e). "Ek kanıtlar" bölümü, istisna seçeneği ve iki yeni alan **bugünkü kiracı düzeyindeki sayfaya** eklenir. Katman listesi çizilmez. | WEB-b |
| D-2 | M2 | Platform "Web tarayıcı · Safari (iPadOS 18)" | Web vekili, kullanıcı ajanından kısa bir tarayıcı özeti çıkarır (ör. "Safari 18 · iPadOS", ≤ 40) ve `appVersion` olarak yazar. İstemcinin gönderdiği `appVersion` web'de yok sayılır. Mobilde `appVersion` uygulama sürümüdür. | BE-c |
| D-3 | S2 / S3 / S6 | Başlatmadan **önce** "Acıbadem Altunizade Hastanesi · kod doğrulandı", "başka bir kuruma ait (Ankara Şehir Hastanesi)"; zorunlu + kurumda kod yok → cezasız başla | Yazmayan ön kontrol ucu: `POST api/crm/visit-execution/qr-check`. Ayrıntı WP §2.10. Kesin karar yine başlatmada sunucuda. | BE-c + WEB-a |
| D-4 | Q1 geçmiş | "Yazdırıldı · Sürüm 3 · A4, 2 adet" | Yazdırma uçları `paper` (`a4`, `a5`, `labels4`, `labels8`) ve `copies` (1–50) alır; `printed` olayına yazılır. Değer yoksa olay eskisi gibi. | BE-c |
| D-5 | Q5 | Satırda üst kurum (↳), "Yok · üst kurum kodu geçer", son 30 gün okutma, bölge süzgeci | Liste satırına `parentAccountId` / `parentAccountName`, `inheritedFrom` (üst kurumun aktif kodu: ad + sürüm), `recentScanCount` eklenir. Şehir adı Web'de şehir BRD'sinden çözülür; bilinen sözlük farkı veri sahibi maddesi olarak kalır. | BE-c |
| D-6 | M1 / M2 | Satırda QR sonucu ("İptal edilmiş QR", "QR okutulmadı"); çekmecede QR sonucu, kurum adı, sürüm, okutma saati, slayt süresi + slayt sayısı, fotoğraf saati | **CT-2:** işaret satırı `qr` alanı gerçek QR özetiyle dolar: durum, kurum kimliği + **adı**, sürüm, okutma anı, eşleşme yolu. Ayrıntı ucu `slideSeconds`, `slideCount`, `photoCapturedAt` ekler. "QR okutulmadı" çipi işaret değildir: `qr.status = missing` iken Web çizer, işaret süzgecine girmez. | BE-c + WEB-b |
| D-7 | — | — | **CT-1 (fail-closed):** işleyicide doğrulayıcı yokken etkin `qrMode != off` ise başlatma 503 `visit_qr_signing_unavailable` döner. DI kaydını ölçen test eklenir. | BE-c |
| D-8 | Q1b | Bulanık önizleme, "Tam kod yazdırmada" | Önizleme **süs**: gerçek yük çekilmez. Yük yalnız yazdırmada, `manage` izniyle istenir. | WEB-a |
| D-9 | Q2 | Tarayıcı yazdırma, QR görseli | QR görseli **Web sunucusunda SVG** olarak üretilir. MIT lisanslı .NET paketi (ör. QRCoder), sürüm sabit, depo bağımlılık kuralına uygun. Uç `no-store`; yük günlüğe yazılmaz. CDN YOK. | WEB-a |
| D-10 | S1 | Kamera çerçevesi | Tarayıcıda `BarcodeDetector` varsa o kullanılır; yoksa wwwroot'a konmuş jsQR (Apache-2.0, sürüm sabit, CDN YOK). Telefonda arka kamera. Okunan metin olduğu gibi gönderilir. | WEB-a |
| D-11 | M1 | Dönem seçici "Son 7 gün · Son 30 gün · Ekim 2026"; "Diğer (7 tür)" | Hazır dönemler bunlar (varsayılan son 7 gün; en çok 92 gün). "Diğer" yalnız ekranda gruplanır. | WEB-b |
| D-12 | S7 | Web'de "Bu cihaz doğrulanamıyor…" | Satır yalnız `contract.policy.antiFraudMode = flag` iken çizilir; başlatmayı engellemez. | WEB-a |
| D-13 | Q1a / Q1c | "QR kodu üret", "Yeni kod üret" | İkisi de `issue` ucu. İptalden sonra sürüm + 1. | WEB-a |
| D-14 | menü | CRM › Ziyaret › "Kurum QR kodları", "Kanıt işaretleri"; Ayarlar › "Ziyaret kanıtı" | Manifest sayfaları BE-a / BE-b'de var (sıra 69 / 71). Web rotaları `/CRM/VisitQrCodes`, `/CRM/VisitEvidenceFlags`. | WEB-a / WEB-b |

## 3. Mockup veri tutarsızlıkları (yok sayılır)
- Q5'te "Ege Üniversitesi Tıp Fakültesi · İptal · v2" iken Q2b etiket sayfasında "Sürüm 3" basılmış. Etiket sayfası yalnız **aktif** kodları basar (`bulk-payload`).
- Q2b'de "Kadıköy" satırı ayrı başlık gibi görünüyor; doğrusu kurum adı tek satır "Eczane Yıldız (Kadıköy)".

## 4. Ekran → uç eşlemesi (WEB için)

| ekran | uç (CRM, Web vekili üzerinden) |
|---|---|
| Q1 kart | `GET api/crm/visit-qr/accounts/{id}`; geçmiş `…/{id}/history` |
| Q2 tekli | `GET …/{id}/payload?paper=&copies=` → Web SVG |
| Q2 etiket | `POST bulk-payload` `{ accountIds, paper, copies }` → Web SVG'ler |
| Q3 / Q4 | `POST …/{id}/issue` `{ expectedVersion, reason? }` · `POST …/{id}/revoke` `{ expectedVersion, reason }` |
| Q5 | `GET accounts?status=&accountType=&city=&territoryId=&search=&page=&pageSize=` · `POST bulk-issue` |
| E | `GET` / `PUT api/crm/visit-evidence-policy` (`qrMode`, `antiFraudMode`) · `…/effective` · `…/reps` · `…/history` · istisna kuralı `qr_optional` |
| S | `GET api/crm/visit-execution/contract` (`policy.qrMode`, `policy.antiFraudMode`) · `POST …/qr-check` · `POST …/start` (`qrPayload`, `qrScannedAt`) |
| M | `GET api/crm/visit-evidence-flags` · `…/summary` · `…/{executionId}`; fotoğraf küçük resmi mevcut kanıt okuma yolu |
