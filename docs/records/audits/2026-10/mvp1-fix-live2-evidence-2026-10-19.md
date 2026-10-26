---
id: mvp1-fix-live2-evidence-2026-10-19
title: WP-MVP1-FIX-LIVE2 — canlı tur bulguları (RC-2026-0001) + 0176 / 0177 dil tamamlama — kanıt
status: agent-result-pending-ct
date: 2026-10-19
modules: [MOD-0177, MOD-0176, MOD-0173]
branch: fix/mvp1-live2
base: 5decde3fb (MVP-1 ucu)
---

# WP-MVP1-FIX-LIVE2 — kanıt (ajan sonucu, CT bekliyor)

Dal `fix/mvp1-live2` (MVP-1 ucu `5decde3fb`), yalıtılmış worktree `erp-vnext-wt-live2`. Push yok. Risk: YÜKSEK (GxP kanıtı).
CT kararları: "yazı tipi A, ad okumaları A" → "çeviri kaynağı B, 0176 kişi adları A" → "dört dosyanın dördü A + ön izin".

## 1. Commit'ler

| commit | tür | içerik |
|---|---|---|
| `bd50db68b` | **owner step MOD-0173 (upstream okumaları)** | `SkuReference.CanonicalCode` (ekleme), `ProductMasterHttpClient.GetSkuAsync` canonicalCode, `ILegalEntityMasterClient` / `LegalEntityMasterHttpClient.GetLegalEntityNameAsync` (salt okunur, MDM `GET /api/legal-entities/{id}`, çağıranın jetonu, aynı gateway yolu); `UpstreamDisplayReadsTests` |
| `b757e0381` | **owner step MOD-0176 (kişi adları)** | `ExpiryESignSignature.SignerNameAtSign / MeaningText` (ekleme), `GatewayExpiryESignClient` `signer.name`, `IExpiryAuditNames` + `ExpiryAuditNames` (merkezi denetim kuyruğu, salt okunur), DI tek satır; `ExpiryPersonNameReadTests` |
| `d8d6c13fb` | **owner step MOD-0176 (kanıt)** | FEFO Kanıt Paketi: servis resx ailesi `ExpiryEvidenceText_{7}`, adlar, 7 dil, zh/ar İngilizce PDF + not, CSV ad sütunları; `ExpiryController` dil kuralı; Web `Expiry/evidence.js` Accept-Language |
| `da6bc370b` | **owner step MOD-0173 (makbuz)** | `ReceiptAsync` lot numarası (TRACE, `trace.lots.read`), makbuz modeli / görünümü, `LotUnresolvedHint` 7 dil |
| `663fe6c52` | MOD-0177 | Recall Kanıt Paketi (adlar, 7 dil, mühür adları, etkinlik), ekran bulguları 4 / 6 / 7 / 8, parite bekçisi |
| `10f9e0ab8` | **owner step MOD-0176 (kişi adları + yardım metinleri)** | `ExpiryCollections.CentralAuditOutbox` (salt okunur sabit), Help.EXPIRY* 48 anahtar × 7 dil |
| `cef1345cc` | MOD-0177 | `RecallCollections.CentralAuditOutbox`; tatbikat kanıt testi yeni imza sütunları |
| `de67eab9a` | **owner step MOD-0176 (kanıt, önizleme bulgusu)** | depo "kod · ad" ad koda eşitse bir kez |
| `6edef15e1` | MOD-0177 | aynı düzeltme Recall kanıtında |
| (bu commit) | docs | bu kanıt + MOD-0177 / MOD-0176 pack §19 notları + FU satırları |

## 2. Bulgular 1–9 — önce / sonra

| # | bulgu (canlı tur) | önce | sonra | test |
|---|---|---|---|---|
| 1 | kişiler kimlik (Açan, Kapsamı onaylayan, Etkinliği tamamlayan, Kapatan, zaman çizelgesi Kişi, imzalar İmzalayan) | GUID | ad: imzalayan = MOD-0022 `signerNameAtSign`; olay kişisi = olayın `ActorName`'i (yeni olaylar) ya da merkezi denetim kuyruğunun `ActorDisplayName`'i (aynı korelasyon + aynı kişi); yoksa `kimlik (ad okunamadı)`; kimlik sütunu yanında (CSV / ekran) | `A_person_is_the_signer…never_another_events_name`, `A_new_event_records_the_name…`, `ExpiryPersonNameReadTests` (iki kişi / iki olay) |
| 2 | şirket, ürün, SKU, depo, lokasyon kimlik | GUID | şirket `GM-EU · Grand Medical EU` (MDM), ürün `kod · ad`, SKU kodu (PRODUCT-MASTER), depo / lokasyon `kod · ad` (0173 ana verisi); okunamayan → `kimlik (okunamadı)` | `Values_names_and_seals…`, `An_unreadable_master_name…`, `The_FEFO_pack_prints_names…` |
| 3 | değerler İngilizce (Closed, Wholesale, Both, Lot/Input/Stock, Held, Opened/TraceRun…, APPROVE) | enum adı | seçilen dilde, ekranın kendi anahtarlarıyla (servis resx'i, parite bekçisi) | `Values_names_and_seals…`, `Every_stored_code_the_pack_words…`, `Every_language_has_a_PDF…` |
| 4 | mühür adları tutarsız (iletişim kutusu "Gönderilen / Onaylanan iz" = ceef06e7…, Vaka Detayı "Mühür" ceef06e730aa, PDF "İzleme mührü" d3b88e31…) | 3 ad, 2 değer karışık | bkz. §3 tablo; aynı değer aynı adla üç yerde | `Values_names_and_seals…` (PDF = rc-v1 = vaka), Node `finding 4` |
| 5 | Bölüm 5 "Etkinlik: Kayıt yok" (tamamlanmış, sevkiyatsız, hedef %100) | "Kayıt yok" | "Etkinlik kontrolü sonucu": durum, hedef, tamamlayan, zaman, imza, not + "Sevkiyat yok — ölçülecek miktar yok" | `A_completed_check_without_shipments…` |
| 6 | "Taslak vaka aç" İzleme'de kalıyor | toast (numara boş: yanıt zarflı) | yeni vakanın **Vaka Detayı**na gider (sonraki adım — kapsam incelemesi / gönderim — orada; toast tıklanamaz ve kaybolur) | Node `finding 6`, `Finding_6_…` |
| 7 | "İadeyi bağla" kapsamda müşteri yokken boş liste | boş seçim kutusu + düğme | açıklama (7 dil) "Kapsamda müşteri yok; iade yalnız onaylı kapsamdaki bir müşteriye bağlanabilir. Müşteriler satış sevkiyatlarından gelir."; alanlar ve "Bağla" düğmesi çizilmez (`d-none`, disabled değil) | Node `finding 7`, `Finding_7_…` |
| 8 | "Müşteri iadesi gir" → 0173 formu ürün kimlik | itemId / sku yalnız | bağlantı `itemText` / `skuText` taşır (vakanın gösterdiği etiket; etiket kimlikse gönderilmez); `ForCreate` okur, yalnız görüntü | Node `finding 8`, `Finding_8_…` |
| 9 | 0173 makbuzu ürün ve lot kimlik | `LotId` | lot NUMARASI (TRACE listLots, SKU başına bir çağrı, kullanıcının kendi `trace.lots.read`'i — FIX-LIVE1 deseni); okunamazsa kimlik + "lot numarası okunamadı"; ürün = formun itemText'i (8 ile birlikte) | `The_receipt_names_a_registered_lot…`, `The_receipt_prints_the_lot_number…` |

## 3. Mühür adı ↔ özet ↔ yer (ölçüldü)

| ad (tr / en) | değer | nereden | imzalanır mı (rc-v1) | nerede görünür |
|---|---|---|---|---|
| **Gönderilen kapsam mührü (imzalanan)** / Submitted scope seal (signed) | `RecallCase.SubmittedScope.Sha256` — rs-scope-v1 (`RecallScopeRules.Sha256(items, revision)`), gönderimde mühürlenir | `CaseCommandHandlers.cs` gönderim | **evet** — rc-v1 alanı `submittedSnapshotSha256` (ad rc-v1'de dondurulmuş; içerik kapsam mührü) | e-imza konu satırı "Gönderilen kapsam mührü / Submitted scope seal"; Vaka Detayı Kapsam sekmesi; PDF / CSV / önizleme özet satırı `submittedScopeSeal` |
| **Onaylanan kapsam mührü (imzalanan)** / Approved scope seal (signed) | `RecallCase.ApprovedScope.Sha256` — rs-scope-v1, onayda | `CaseCommandHandlers.cs` onay | **evet** — rc-v1 `approvedSnapshotSha256` | aynı dört yer |
| **İz anlık görüntüsü özeti** / Trace snapshot digest | `RecallTraceSnapshot.Sha256` (rt-v1, izleme sonucu) | `FindSnapshotAsync(approved ?? submitted)` | **hayır** | PDF / CSV / önizleme `snapshotSha256` (eski ad "İzleme mührü" — yanlıştı) |
| **Şu anki kapsam özeti** / Current scope digest | `RecallScopeRules.Sha256(şimdiki kalemler, ScopeRevision)` — canlı | `RecallReads.Scope` | hayır | Vaka Detayı; son saklanan mühürden (en yüksek revizyon) farklıysa uyarı "kapsam mühürlendikten sonra değişti" |

Canlı turdaki karışıklık: iletişim kutusundaki "iz" adları aslında kapsam mührünü gösteriyordu (ceef06e7… = rc-v1 alanları); PDF'teki "İzleme mührü" ise iz
anlık görüntüsünün özetiydi (d3b88e31…). Vaka Detayı "Mühür" canlı kapsam özetiydi (aynı kalemler → aynı değer, ceef06e730aa). Test:
`Values_names_and_seals_are_worded_in_the_asked_language_and_the_signed_seal_is_the_rc_v1_one` — aynı vakada rc-v1 konusu alanı = vaka `approvedScope.sha256` =
PDF "Onaylanan kapsam mührü (imzalanan)" satırı; sabotaj (c) kırmızı.

## 4. Kişi adları — kural ve 0176 notu

- İmzalayan: MOD-0022 `GET /api/v1/esign/signatures/{id}` → `signer.name` (= digest'teki `signerNameAtSign`), çağıranın jetonuyla. Okunamazsa
  durum "Okunamadı (NotFound / Forbidden / Unavailable)", ad / anlam / zaman BOŞ (Recall); FEFO'da "imza-kimliği (ad okunamadı)".
- Olay kişisi (Recall): yeni `RecallCaseEvent.ActorName` (ekleme; `RecallSupport.Event` doldurur — `ICentralAuditScope.ActorDisplayName` ile aynı değer:
  jetonun `name` ya da `given_name` + `family_name` iddiaları). Eski olaylar: `central_audit_outbox` — kiracı + `CorrelationId` + `ActorId` birlikte;
  aynı korelasyonda başka kişinin satırı adını VERMEZ (test: tuzak satır "Yanlış Kişi" hiçbir yerde görünmez).
- Özet kişileri: adımın imzasının imzalayanı (kimlik eşleşirse), yoksa adımın olayının kişisi (kimlik eşleşirse); yoksa `kimlik (ad okunamadı)`.
- **MOD-0176:** yalnız okuma yolu (imza + denetim kuyruğu). **Expiry varlıklarına alan EKLENMEDİ** (`FefoExceptionCase`, `ExpiryPolicyHistoryEntry` — test
  yansıma ile denetler). Politika değişikliğini yapan: kuyruk, `ExpiryPolicyHistoryEntry.CorrelationId` + `ChangedBy` ile.
- İki okuyucu (`ExpiryAuditNames`, `RecallAuditNames`) aynı koleksiyonu aynı sorguyla okur; CT kararı gereği Expiry ↔ Recall bağı kurulmadı.
  **Öneri (O-LIVE2-3):** ortak bir salt okunur okuyucu `Application/Common` + `Persistence` altında (iki modülün de kullanacağı, BuildingBlocks'a dokunmadan).

## 5. Dil tamlığı (anahtar × dil) — ölçüm

**Web Recall (Recall.{lang}.resx)** — 680 anahtar

| dil | anahtar | eksik | boş | İngilizce ile aynı | Help.* |
|---|---|---|---|---|---|
| en | 680 | 0 | 0 | 0 | 53 |
| tr | 680 | 0 | 0 | 7 | 53 |
| fr | 680 | 0 | 0 | 20 | 53 |
| es | 680 | 0 | 0 | 9 | 53 |
| zh | 680 | 0 | 0 | 1 | 53 |
| ar | 680 | 0 | 0 | 1 | 53 |
| ru | 680 | 0 | 0 | 1 | 53 |

**Web Expiry (Expiry.{lang}.resx)** — 447 anahtar

| dil | anahtar | eksik | boş | İngilizce ile aynı | Help.* |
|---|---|---|---|---|---|
| en | 447 | 0 | 0 | 0 | 48 |
| tr | 447 | 0 | 0 | 2 | 48 |
| fr | 447 | 0 | 0 | 10 | 48 |
| es | 447 | 0 | 0 | 1 | 48 |
| zh | 447 | 0 | 0 | 0 | 48 |
| ar | 447 | 0 | 0 | 0 | 48 |
| ru | 447 | 0 | 0 | 0 | 48 |

**Servis RecallEvidenceText_{lang}** — 205 anahtar

| dil | anahtar | eksik | boş | İngilizce ile aynı | Help.* |
|---|---|---|---|---|---|
| en | 205 | 0 | 0 | 0 | 0 |
| tr | 205 | 0 | 0 | 4 | 0 |
| fr | 205 | 0 | 0 | 12 | 0 |
| es | 205 | 0 | 0 | 4 | 0 |
| zh | 205 | 0 | 0 | 1 | 0 |
| ar | 205 | 0 | 0 | 1 | 0 |
| ru | 205 | 0 | 0 | 1 | 0 |

**Servis ExpiryEvidenceText_{lang}** — 74 anahtar

| dil | anahtar | eksik | boş | İngilizce ile aynı | Help.* |
|---|---|---|---|---|---|
| en | 74 | 0 | 0 | 0 | 0 |
| tr | 74 | 0 | 0 | 2 | 0 |
| fr | 74 | 0 | 0 | 5 | 0 |
| es | 74 | 0 | 0 | 1 | 0 |
| zh | 74 | 0 | 0 | 0 | 0 |
| ar | 74 | 0 | 0 | 0 | 0 |
| ru | 74 | 0 | 0 | 0 | 0 |

- Eksik / boş: **0**, dört ailede, 7 dilde. Help.* (UXH-001): Recall 53 × 7 (R6'dan), **Expiry 48 × 7 (bu WP; 8 sayfa × Purpose / Audience / 2 adım)**.
- İngilizce ile aynı değerler gözden geçirildi: hepsi dilin kendi sözcüğü / kod / biçim (fr "Page", "Type", "Lot", "Signature"; tr "Lot", "No"; es "No",
  "Normal"; SKU). Gerçek çeviri gereken üç tr değeri düzeltildi: `Expiry.Col.Sla` "Süre (SLA)", `Expiry.Policy.SlaOf` "Süre · {0}", `Expiry.Policy.Retest`
  "Yeniden test". İzin listelerinden eskiyen girdiler çıkarıldı (Expiry: Col.Unit, Order.SeeQuarantine, Modal.Type.* ×4, Col.Sla, Policy.SlaOf,
  Policy.Retest; Recall: Ev.W.skuId); eklenen tek girdi `Recall.Ev.W.no` (es "No").
- zh / ar lot sözcükleri: "批次" / "التشغيلة" (Recall.Kind.Lot, Ev.W.lot, StCol.Lot, Drills.Lot; ExpiryPdf.Lot).
- JS l10n dosyaları (`recall.l10n.js`, `expiry.l10n.js`) metin taşımaz; anahtar listesini (`Recall.Keys`, `Expiry.Keys`) yükler — tamlık resx'te ölçülür.
- Parite bekçisi (`EvidenceTextParityTests`): servis ailesinin her paylaşılan anahtarı Web'de aynı değerle (7 dil) ve Web'in paylaşılan gruplarındaki her
  anahtar serviste var (iki yönlü); yalnız-dosya anahtarları `RecallPdf.*` / `ExpiryPdf.*`, 7 dilde dolu. Sabotaj (i) kırmızı.

## 6. PDF dili ve yazı tipi — ölçüm

Gömülü yazı tipi: PDFsharp-MigraDoc 6.1.1 `FontDataHelper.SegoeWP` (62 388 bayt, cmap 646 kod noktası; format 4 / 12 okunarak ölçüldü).

| dil | örnek | cmap | PDF |
|---|---|---|---|
| tr | ğşİıçöü | tamam | tr |
| en | Recall | tamam | en |
| fr | éèàçœ | tamam | fr |
| es | ñáéíóú¿ | tamam | es |
| ru | ЖжЩщЫы | tamam | ru |
| zh | 批次召回证据 | **eksik** U+6279 U+6B21 U+53EC U+56DE U+8BC1 U+636E | İngilizce + not |
| ar | التشغيلة | **eksik** U+0627 U+0644 U+062A U+0634 U+063A U+064A U+0644 U+0629 | İngilizce + not |
| ar (sunum biçimleri) | ﺍﻟﺗ | **eksik** U+FE8D U+FEDF U+FE97 | — |
| noktalama | — – · … ’ “ ” → % № | tamam (yalnız ✓ U+2713 eksik — kullanılmıyor) | — |

- **Arapça şekillendirme bulgusu:** PDFsharp / MigraDoc 6.1.1 Arapça harf birleştirme (shaping: başta / ortada / sonda biçimler) ve sağdan sola
  dizilim (bidi) YAPMAZ; glif bulunsa bile Arapça metin ayrık ve soldan sağa çizilir. Bu, yalnız yazı tipi eklemekle çözülmez.
- **Glif sondası (scratchpad `glyphprobe`):** zh / ar metin PDFsharp'ta hata vermeden çizilir ama her karakter boş kutu (.notdef) olur ve metin olarak
  çıkarılamaz. Bu yüzden CT kararındaki "not seçilen dilde VE İngilizce (iki satır)" PDF içinde **çizilemiyor** (O-LIVE2-1): PDF'in üstündeki iki satır
  İngilizce not + "Requested language: zh|ar"; seçilen dildeki not ekranda gösterilir (Recall Kanıt Paketi sonuç kutusu `Recall.Ev.W.englishFallback`,
  FEFO Kanıt Paketi bildirimi `Expiry.Evidence.EnglishFallback`, 7 dil). CSV ve önizleme seçilen dilde (zh / ar dahil).
- FU-0176-PDF-L10N daraltıldı ve açık: "zh / ar PDF: yazı tipi (OFL) + Arapça şekillendirme / sağdan sola dizilim — lisans ve kütüphane kararı";
  recall PDF'ini de kapsar (iki pakette yazıldı).
- Test: `Every_language_has_a_PDF_tr_en_fr_es_ru_in_their_own_words_zh_and_ar_in_English_with_the_note` (Recall),
  `Every_language_has_a_FEFO_PDF_and_zh_or_ar_is_English_with_the_note` (FEFO) — 7 dilde PDF üretilir; 5 dilde başlık / durum o dilde; zh / ar İngilizce +
  not + istenen dil; CSV o dilde. SHA-256 her dışa aktarmada indirilen baytların özeti.
- Ölçüm notu: testlerin kendi küçük PDF okuyucusu "—" (U+2014) karakterini farklı çıkarıyor; yazı tipinde glif VAR (cmap). Testler bu karakterde harf /
  rakam karşılaştırması yapar.

## 7. Sabotaj (her biri: uygula → derle → filtre KIRMIZI → özgün baytlar + touch → derle → filtre YEŞİL)

| # | sabotaj | kırmızı | geri alındı |
|---|---|---|---|
| a | özet kişisi ad yerine kimlik (`Person` → `return id`) | 2 / 7: `A_new_event_records_the_name_of_the_user_from_the_token`, `A_person_is_the_signer_at_signing_or_the_audit_name_of_the_same_event_and_never_another_events_name` | 7 / 7 |
| b | ad okunamayınca uydurma ad (`NameUnreadable` → "Bilinmeyen Kullanici") | 1 / 7: `A_person_is_the_signer…` | 7 / 7 |
| c | PDF'te imzalanan (onaylanan) mühür rc-v1 dışı özet (iz anlık görüntüsü) | 1 / 7: `Values_names_and_seals_are_worded_in_the_asked_language_and_the_signed_seal_is_the_rc_v1_one` | 7 / 7 |
| d | vaka durumu İngilizce kalır | 2 / 7: `Values_names_and_seals…`, `Every_language_has_a_PDF_tr_en_fr_es_ru…` | 7 / 7 |
| e | tamamlanmış, sevkiyatsız kontrolde Bölüm 5 sonucu yok ("Kayıt yok"a dönüş) | 1 / 7: `A_completed_check_without_shipments_states_its_result_instead_of_no_record` | 7 / 7 |
| f | Web tr anahtarı İngilizce kopya (`Recall.Link.NoCustomers`) | 1 / 1: `RecallWebTests.Seven_languages_carry_exactly_the_Recall_keys_with_real_text` | 1 / 1 |
| g | makbuz lot numarası yerine kimlik (`Details.cshtml`) | 1 / 10: `MovementPagesSurfaceTests.The_receipt_prints_the_lot_number_and_an_unresolved_lot_as_its_id_with_the_hint` | 10 / 10 |
| h | `GetLegalEntityNameAsync` hata yanıtında uydurma ad | 5 / 16: `UpstreamDisplayReadsTests.Every_answer_that_is_not_a_clean_name_is_null_and_never_invented` (401, 403, 404, 500, 503) | 16 / 16 |
| i | SERVİS resx'inde paylaşılan değer değişti (tr `Recall.Status.Closed` "Kapalı") | 1 / 4: `EvidenceTextParityTests.Every_shared_word_is_the_screens_word_both_ways_in_seven_languages(Recall)` | 4 / 4 |

Araç: `scratchpad/live2/sabotage-live2.js` (R7c aracı, sabotaj başına filtre).

SHA-256 (dokunulan her dosya) = başlangıç: **true**; `git status --porcelain` aynı: **true**.

## 8. Sayılar

| ölçüm | önce (`5decde3fb`) | sonra |
|---|---|---|
| Web | 2562 / 2562 | **2575 / 2575** (+13) |
| Inventory | 1250 + 4 atlanan, 1 kırmızı — `ShipmentReaderTests.At_most_eight_reads_are_in_flight` (yük altında zaman aşımı; tek başına 11 / 11 — bilinen sallantı) | **1278 + 4 atlanan, 0 kırmızı** (+27) |
| 0173 Web (`Diten.Web.Tests.Inventory.`) | 191 / 191 | **193 / 193** (+2 makbuz) |
| 0174 Web (`Traceability.`) | 110 / 110 | **110 / 110** |
| 0175 Web (`Quarantine.`) | 37 / 37 | **37 / 37** |
| 0176 Web (`Expiry.`) | 76 / 76 | **77 / 77** (+1 yardım metni; izin listesi sıkılaştı) |
| 0177 Web (`Recall.`) | — | **292 / 292** (yeni: parite bekçisi 4, LIVE2 ekran 5 + Node köprüsü 1) |
| Node (`recall-live2.test.mjs`) | — | 4 / 4 |
| Mimari | 71 / 79, 8 kırmızı | aynı 8, iletiler birebir (sıralı karşılaştırma) |
| Gateway | 164 / 165 (`EveryRoute_DownstreamPortIsInKnownServiceSet`) | aynı |
| Derleme uyarısı | Web 24, Inventory 0 | Web 24, Inventory 0 |
| Denetimsiz yazma komutu (Inventory) | 0 | 0 — yeni yazma komutu yok; `Every_write_command_declares_its_central_audit_record` yeşil |

## 9. Ön izinle yapılan beyan dışı değişiklikler (CT ön izni: MOD-0173 / 0176 / 0177 içinde dar ve EKLEMELİ)

| dosya | satır (sonrası) | modül / commit | neden | davranış |
|---|---|---|---|---|
| `services/…/Application/Features/Expiry/ExpiryEvidenceText.cs` (yeni) | 1–44 | 0176 / `d8d6c13fb` | servis resx ailesini okuyan yardımcı (CT "çeviri kaynağı B"); Recall'ınkini kullanmaz (Expiry ↔ Recall bağı yok) | yeni; elle `Labels` tablosu silindi |
| `services/…/Application/Features/Expiry/ExpiryEvidenceText_{en,tr,fr,es,zh,ar,ru}.resx` (yeni) | tümü | 0176 / `d8d6c13fb` | FEFO kanıt sözcükleri (beyanlı "Expiry resx" kapsamında; servis tarafı) | yeni |
| `services/…/Persistence/Expiry/ExpiryCollections.cs` | 10–13 | 0176 / `10f9e0ab8` | `CentralAuditOutbox` sabiti — mimari kural "GetCollection beyanlı sabit"; `All` kümesine EKLENMEDİ | yok (sabit) |
| `frontend/Diten.Web/Views/Expiry/Expiry.cs` | 366–414 | 0176 / `d8d6c13fb`, `10f9e0ab8` | `Expiry.Keys` listesine `Expiry.Evidence.EnglishFallback` + 48 Help.EXPIRY* (l10n anahtar listesi) | yalnız ekleme |
| `frontend/Diten.Web/Resources/Views/Inventory/StockMovements/StockMovementsIndex.{7}.resx` | sona 1 satır | 0173 / `da6bc370b` | `LotUnresolvedHint` (Stok Defteri ile aynı metin, 7 dil) — makbuz ipucu | yalnız ekleme |
| `services/…/Tests/TestInfrastructure/StubLegalEntityMaster.cs` | 18, 30–32, 59–66 | 0173 / `bd50db68b` | arayüzün yeni metodu; ad kaydı | test altyapısı |
| `services/…/Tests/TestInfrastructure/StubProductMaster.cs` | 31–34, 85–87, 102–103 | 0176 / `d8d6c13fb` | `SetDisplay` (canonicalCode / name) | test altyapısı; var olan çağrılar aynı |
| `services/…/Tests/TestInfrastructure/FakeESignGateway.cs` | 25, 41, 45, 79, 81 | 0176 / `b757e0381` | sahte MOD-0022'ye `signer.name` / `meaningText` (varsayılan "Signer" aynı) | test altyapısı |
| `frontend/Diten.Web.Tests/Expiry/ExpiryWebTests.cs` | 33–37, 60–69 | 0176 / `d8d6c13fb`, `10f9e0ab8` | İngilizce-aynı izin listesi gözden geçirme; yardım metni testi | sıkılaştırıldı |
| `services/…/Tests/Expiry/ExpiryEvidenceTests.cs` | 8, 210–212, 255, 269–360, 365 | 0176 / `d8d6c13fb` | CSV başlığı (sona eklenen sütunlar), bölüm başlığı Web anahtarıyla, yeni ad / dil testleri, `PdfText` internal | değişen davranış testle gösterildi |
| `frontend/Diten.Web.Tests/Inventory/MovementFormTests.cs` | 191–223 | 0173 / `da6bc370b` | makbuz lot numarası testi | yeni test |
| `frontend/Diten.Web.Tests/Inventory/MovementPagesSurfaceTests.cs` | 119–134 | 0173 / `da6bc370b` | makbuz görünümü testi | yeni test |

MOD-0177 dosyaları (bu WP'nin modülü) izin listesine girmez; Platform / Auth / MDM / Gateway / BuildingBlocks / başka modül: **dokunulmadı**
(MDM `LegalEntitiesController` yalnız OKUNDU; `CentralAuditOutboxRecord` yalnız okunuyor). Yeni paket / kütüphane / yazı tipi: **yok**.
Sözleşme: RECALL `EvidencePreview` `additionalProperties: true` — satır içerikleri sözcüklendi, şekil aynı; FEFO CSV sütunları sona eklendi (eklemeli).

## 10. Önizleme

Düzenek `scratchpad/live2-preview` (R7c'ninki): gerçek InventoryService test sunucusu (kendi Mongo replika seti, çalışma başına rastgele test
anahtarı — dev-secret YOK) `127.0.0.1:5168`; önizleme ağ geçidi `127.0.0.1:5105`; üretim Diten.Web `127.0.0.1:5104` (qa1). Filo portlarına
(5000–5070, 4010), 5011 / 5004'e, "Diten ERP - …" pencerelerine dokunulmadı. İndirmeler tarayıcıyla DEĞİL, ağ geçidinden scratchpad'e
(`live2-preview/downloads`) alındı. Tohum gerçek API'lerle: C1 `RC-2026-0001` KAPALI (canlı turun şekli; üç imza sahte MOD-0022'de imza anındaki
adla, kuyruk satırlarında kullanıcı adları), C2 `RC-2026-0002` aktif ve müşterisiz, C3 aktif ve sevkiyatlı. Şirket / ürün / SKU adları test
çiftlerinden.

| ne | sonuç |
|---|---|
| Vaka Detayı → Kapsam (C1) | "Gönderilen kapsam mührü (imzalanan) 2d55655a21dc", "Onaylanan kapsam mührü (imzalanan) 2d55655a21dc", "Şu anki kapsam özeti (SHA-256) 2d55655a21dc" (`data-hash` tam değer üçünde aynı) |
| Kanıt Paketi önizlemesi (tr) | Özet: Durum Kapandı, Düzey Eczane, Yön İleriye, Ürün `GP-0042 · Asetilsalisilik asit 100 mg tablet`, SKU kodu `LS-0042-30`, Şirket `GM-EU · Grand Medical Europe B.V.`, Açan / Kapsamı onaylayan / Etkinliği tamamlayan / Kapatan adlar (kimlik ayrı satırda); İzleme: "Bitmiş ürün lotu", "Depo stoğu", "Sevkiyat"; Karantina: "Karantinada"; Bölüm 5: "Etkinlik kontrolü sonucu" (tamamlandı, hedef %95, Ayşe Demir, imza · ad, not); Zaman çizelgesi: "Vaka açıldı" … "Vaka kapandı", Kişi adları; İmzalar: işlem adları, imzalayan adları, "Onay", "Geçerli" |
| PDF tr / en / fr / es / ru / zh / ar + CSV tr / zh (C1) | 9 / 9 dışa aktarma: indirilen baytların SHA-256'sı = günlükteki (`exports.json`). tr PDF: tüm sözcük / değer / ad Türkçe, mühür adları §3'teki gibi. zh / ar PDF: İngilizce + üstte "This document was produced in English (the selected language is not supported in the PDF)" / "Requested language: zh|ar". zh CSV: Çince başlık ve değerler ("召回证据包", "状态;已关闭") |
| Bulgu 8 (C2 → "Müşteri iadesi gir") | bağlantı `…&itemText=GP-0042+·+Asetilsalisilik+asit+100+mg+tablet&skuText=LS-0042-30&lotId=…&toStatus=QUARANTINE`; form ön dolu: Müşteri iadesi, ürün metni, SKU metni, lot `P2-4E6CD226 · Serbest`, Karantina |
| Bulgu 9 (makbuz) | `INV-2026-0000013`: Ürün `GP-0042 · …`, SKU `LS-0042-30`, Lot numarası `P2-4E6CD226` (iki yerde; kimlik değil) |
| Bulgu 7 (C2 → "İadeyi bağla") | "Kapsamda müşteri yok; iade yalnız onaylı kapsamdaki bir müşteriye bağlanabilir. Müşteriler satış sevkiyatlarından gelir."; müşteri alanı ve "İadeyi bağla" düğmesi `d-none` (disabled DEĞİL) |
| Bulgu 6 (İzleme → "Taslak vaka aç") | gönderimden sonra adres `/Recall/Cases/Details/683cfb68-…`, başlık `RC-2026-0004` |

Önizlemeden sonra kendi süreçlerim (5104 / 5105 / 5168) `taskkill /T` ile kapatıldı; önizleme mongod'u kendiliğinden çıktı, kalan tek mongod
sahibin servisi (PID 5264, dokunulmadı); junction'lar (`docs`, `services`) kaldırıldı.

**Önizleme bulgusu (düzeltildi, `de67eab9a` / `6edef15e1`):** tohum lokasyonları koduyla adlandırılmış → "WH-x · WH-x"; "kod · ad" ad koda eşitse
bir kez yazılır (test).
**Önizlemede görülen, kapsam dışı (CT'ye):** Vaka Detayı başlığında "Açan: açılış &lt;kimlik&gt;" — ekran başlığı, kanıt paketi değil (O-LIVE2-6);
"Karantina isteği gönderildi" olayının metni "Held: P2-… &lt;depo kimliği&gt;/&lt;lokasyon kimliği&gt;" — kayıt anında yazılmış serbest metin (olayın kendi
değeri, çevrilmez / değiştirilmez) (O-LIVE2-7).

## 11. CT'ye açık noktalar

- **O-LIVE2-1 (yazı tipi A'dan sapma, zorunlu):** zh / ar notunun seçilen dildeki satırı PDF'te çizilemiyor (glif yok, metin çıkarılamıyor — ölçüldü).
  Uygulanan: PDF'te iki satır İngilizce (not + istenen dil kodu); seçilen dildeki not ekranda. Alternatif yalnız FU-0176-PDF-L10N'nin yazı tipi kararıyla.
- **O-LIVE2-2:** FEFO CSV'de ad sütunları "yanına" değil **sona** eklendi (`…;imza;urun_adi;sku_kodu;depo_adi;tur_adi;durum_adi;cozum_adi;imzalayan;imzalayan_kimlik`):
  dokuz sütunun yeri değişmesin (konuma göre okuyan tüketici kırılmasın — eklemeli). Recall CSV'de kimlik sütunları adların hemen yanında
  (tablo sütunları; özet satırlarında `…By` / `…ById` art arda).
- **O-LIVE2-3:** iki denetim-ad okuyucusu (Expiry / Recall) aynı sorgu — ortak okuyucu önerisi (§4).
- **O-LIVE2-4:** FEFO kanıtında imzalayan adı için kullanıcının `esign.signatures.read` anahtarı gerekir (imza çağıranın jetonuyla okunur, MOD-0175
  kuralı); anahtarsız kullanıcı "imza-kimliği (ad okunamadı)" görür. Recall'da `recall.evidence.export` ile birlikte zaten isteniyordu.
- **O-LIVE2-6 / 7:** önizlemede görülen, kapsam dışı — Vaka Detayı başlığında Açan kimlik (ekran başlığı; öneri FU-0177-HEAD-NAMES); karantina olayının serbest metni kimlik içerir (kayıt anındaki metin).
- **O-LIVE2-5:** anlam (meaning) yalnız `APPROVE` kodu için sözcüklendi (envanter modüllerinin tek kodu); başka kod → MOD-0022'nin kaydettiği `meaningText`,
  o da yoksa kod.

## 12. diff-stat

93 dosya, +4864 / −380 (kod + test, docs hariç; docs commit'i ile birlikte git log'da)
