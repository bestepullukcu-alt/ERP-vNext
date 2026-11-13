# Mobil karşılaştırma istemi — CRM saha akışları (Web + backend) ↔ Android uygulaması · 2026-10-23

> **Bu belgenin tamamını mobil ekibin ajanına yapıştırın.** Kendi kendine yeterlidir: bizim repoya erişim gerekmez. Alan ve kod adları koddaki gibi İngilizce bırakıldı.

---

## İSTEM

Sen Diten ERP'nin Android saha uygulamasının geliştiricisisin. Aşağıda, CRM ekibinin (Web + backend) **saha temsilcisi akışlarında bugüne kadar yaptığı her şey** anlatılıyor: ekranlar, uçlar, alanlar, iş kuralları, hata kodları, izin anahtarları ve **2026-10-23 tarihli canlı bir yerel test akışından gerçek örnek veriler**.

**Görevin:**
1. Kendi uygulamanın kodunu (ekranlar, API istemcileri, modeller) bu listeyle **madde madde** karşılaştır.
2. Her madde için durumu yaz: **Var** (aynı uç / alanlarla tam) · **Kısmen** (ne eksik, hangi alan / kural farklı) · **Yok**.
3. Kullandığın uç veya alan bizimkinden farklıysa (eski uç, eski alan adı, eksik başlık, farklı hata kodu yorumu) **farkı açıkça yaz**.
4. Her bölüm için kapsama yüzdesi ver (Var = 1, Kısmen = 0,5, Yok = 0; madde sayısına böl).
5. Bizden (backend) istediğin **yeni uç / alan / düzeltmeleri** ayrı listede ver. Her biri için gerekçe ve öncelik (yüksek / orta / düşük).
6. Belirsiz kalan her şeyi **soru** olarak yaz; tahmin etme.
7. Hiçbir şeyi değiştirme. Yalnız oku ve raporla. Kod yazman istenmedi.

**Çıktı biçimi (aynen bu başlıklarla, Türkçe):**
```
# Android karşılaştırma raporu — <tarih>
## Özet
- Uygulama sürümü / dal / commit:
- Genel kapsama: %NN  (bölüm bazında tablo aşağıda)
## Bölüm bazında kapsama
| # | Bölüm | Madde | Var | Kısmen | Yok | Kapsama |
## Ayrıntılı karşılaştırma
### 1. Planlama ... (her bölüm için)
| Madde | Bizde (Web/backend) | Mobilde | Durum | Fark / not |
## Backend'den istekler
| # | İstek | Gerekçe | Öncelik |
## Sorular
## Mobilde olup bizde olmayanlar (varsa)
```

---

## 0. Ortak kurallar (her uç için)

- **Adres:** bütün uçlar Gateway üzerinden (`http://<gateway>:5000`). Gateway yolu CRM yolu ile aynıdır (`/api/crm/...`, `/api/commercial/...`).
- **Kimlik:** `Authorization: Bearer <JWT>`. Kiracı token'dan okunur (`tenant_id`); başka temsilcinin kaydı **404** döner.
- **Zarf:** `{ data, statusCode, isSuccessful, errors[] }`.
  - CRM ziyaret uçlarında `errors = [mesaj, kod]` (+ bazen ek satır, ör. `distance_meters=1234`).
  - **Ön sipariş ve fiyat uçlarında sıra ters:** `errors = [kod, mesaj]`.
- **Zaman:** her an sunucunun; "bugün" UTC günüdür.
- **Sürüm:** yazmalar `expectedVersion` ile (bazıları `If-Match` da alır). Eski sürüm → **409**.
- **Dil:** ekran metinleri kodla gelir, etiketler Reference Data (BRD) setlerinden.

---

## 1. Haftalık planlama ve Ziyaret Çalışma Alanı (takvim)

**Web'de ne var:** tek takvim ekranı (Ziyaret Çalışma Alanı). "Planla" modunda temsilcinin haftası otomatik önerilir:
- doktorlar segment ve strateji şablonuna göre;
- süre içeriğin uzunluğundan, kapasite çubuğu gün / hafta;
- sağ panelde kurum kurum hedefler sürüklenip bir güne bırakılır;
- **"Haftayı onayla"** planlı ziyaretleri yazar, **"Haftayı yeniden aç"** gerekçeyle geri açar.

"Yürüt" modunda ziyaret kartından **Ziyarete başla / Yapılamadı / Ertele / İptal**; ayrıca bugün için **Plan dışı ziyaret**.

**Planlama oturumu** (`crm.visit-plan.read` · `.generate` · `.apply` + `crm.planned-visit.manage`; başka temsilci için `crm.visit-plan.read-all`):
- `GET api/crm/visit-plan/sessions` (`cyclePeriodId`, `resourceId`, `status`) → oturum listesi.
- `GET …/sessions/{id}`:
  - seçili kurum / eczane / doktorlar ve ürünler `{productId, productCode, productName, role promo|non-promo}`;
  - `weeks[{weekStart, isoWeek, status, visitCount, approvedAt, dayPins…}]`;
  - `version`.
- `POST …/sessions` · `PUT …/sessions/{id}`:
  - seçim + `dayPins{weekStart, pins[{targetType, targetId, contactId, date, scope visit|institution, startTime "HH:mm"}]}`;
  - `weekExtras`;
  - `expectedVersion`.
  - Hatalar: `pin_not_working_day`, `pin_time_invalid` (15 dk ızgara), `pin_time_outside_hours`, `week_already_approved`, `week_in_past`, `extra_*`.
- `POST …/preview` → kuru çalışma. Uyarılar: `pin_overflow`, `capacity_full`, `public_holiday`, `consent_blocked`…
- `POST …/apply` (**haftayı onayla**) `{planningSessionId, weekStart, expectedVersion, visitPurpose, visitType, startLat, startLong}`:
  - yanıt `{committedPlannedVisitIds[], scheduledCount, unscheduledCount, weekStatus:"approved"}`;
  - hatalar `week_already_approved`, `week_in_past`.
- `POST …/sessions/{id}/weeks/{weekStart}/reopen` `{reason ≥10, expectedVersion}`: raporlanmamış ziyaretler `week_reopened` gerekçesiyle iptal olur.
- `GET …/my-accounts`, `GET …/my-accounts/{accountId}/doctors`, `GET …/sessions/{id}/targets?weekStart` → hedef listeleri.

**Takvim okuma** (`crm.visit-report.read` + `crm.visit-plan.read`):
- `GET api/crm/visit-workspace/contract`:
  - `workStatuses`, `reasonSet`, `rescheduleOptionDays`=8, `maxWindowDays`=42, `reportDeadlineHours`=48;
  - `weekStates` (draft / approved / past / none);
  - `errorCodes`, `permissions`.
- `GET …/calendar?from&to&resourceId&lang` (≤42 gün) → `{visits[], weeks[], days[]}`.
  - **Ziyaret kartı:** `plannedVisitId` (taslakta null + `previewKey`), `visitCode`, `plannedDate`, `startTime`, `endTime`, `durationMinutes`, `targetType`, `contactId`, `accountId`, `targetDisplayName`, `specialtyLabel`, `accountDisplayName`, `accountAddress`, `planStatus`, `workStatus`, `reportDeadline`, `reportState`, `managerAttention`, `executionId`, `executionStatus`, `startedAt`, `finishedAt`, `isPinned`, `pinnedTime`, `isExtra`, `source` (route-plan / unplanned …), `badges`.
  - **`plannedContent[{productId, productCode, role, journeyId, journeyCode, stageId, stageIndex, stageCode, stageName, steps[{title, type, minutes, contentId}]}]`.**
  - `weeks[]`: `{weekStart, weekNumber, state, planningSessionId, canApprove, canReopen, capacityMinutes, plannedMinutes, visitCount, unplacedCount, sessionVersion, unplaced[]}`.
  - `days[]`: `{date, isHoliday, holidayName, capacityMinutes, plannedMinutes, freeMinutes}`.
- `GET …/reasons?appliesTo=cancel|missed|reschedule&lang` → `{items[{code, label, requiresNote}]}`.
- `GET …/reschedule-options?plannedVisitId` → sonraki 8 iş günü ve doluluk.

**Planlı ziyaret** (`crm.planned-visit.read` · `.manage` · `.confirm`):
- `POST api/crm/planned-visits` — **plan dışı ziyaret:** `unplanned=true`, **yalnız bugün** (`unplanned_visit_today_only`).
- `POST …/{id}/cancel` `{reasonCode, note ≤500, expectedVersion}`:
  - `visit_reason_note_required`, `visit_cancel_past_day`;
  - başlamışsa `visit_execution_started`.

**Yapılamadı / ertele** (iki adım, `crm.visit-report.record` + `crm.planned-visit.manage`):
1. `POST api/crm/visit-report/outcome` `{plannedVisitId, executionOutcome missed|rescheduled, reasonCode, reasonNote, rescheduleToDate, expectedVersion}`.
2. `POST api/crm/visit-report` ile kesinleştirme; erteleme yeni ziyareti burada yaratır.

Hatalar: `visit_reschedule_date_invalid`, `visit_not_yet_due`, `visit_execution_started`.

**Türetilen `workStatus`** (öncelik sırasıyla): `cancelled` > `not_done` > `rescheduled` > `reported` > `expired` > `report_missing` > `missed` > `in_progress` > `today` > `planned`. Takvimde ek olarak `draft`.

**Önemli kural (2026-10-23 canlı ölçüm):** sunuma içerik **yalnız plandan gelen** ziyarette gelir. İçerik, hafta onaylanırken doktorun strateji şablonundan ziyarete dondurulur. **Plan dışı ziyarette sunum boş kalır** (`warnings:["no_content"]`).

---

## 2. Ziyaret yürütme ve kanıt

(`crm.visit-execution.read` · `.record`)

- `GET api/crm/visit-execution/contract`:
  - **etkin politika** (kiracı ⊕ tüzel kişilik ⊕ iş birimi ⊕ süreli istisna);
  - `visitKinds` (in_person / remote);
  - `markFlags`, `flagCodes`, `locationErrors`;
  - limitler: `maxNoteLength` 2000, `maxPersonalNoteLength` 1000, `maxCoVisitors` 3, `maxEvidenceBytes` 10 MB;
  - `qrModes`, `antiFraudModes`, `clientPlatforms`, `integrityProviders`;
  - `errorCodes`.
- `GET …/by-plan/{plannedVisitId}` → yürütme (başlamadıysa `data:null`):
  - `status` started|finished, `kind`, `startedAt`, `finishedAt`, `durationSeconds`;
  - `location{…, distanceMeters, status ok|far|missing|inaccurate|account_unknown}`, `photo`, `signature`;
  - `productTimings`, `slideTimings`, `marks`, `note`, `coVisitors`;
  - `flags[]`, `qr{…}`, `device{…}`, `reportDeadline`, `exceptionId`, `relaxedRules`.
- `GET …/by-plan/{id}/brief` → "bu doktor için" kısa özet: son ziyaret, geri bildirim, numuneler.
- **`POST …/start` (multipart).** Alanlar:
  - `plannedVisitId`, `kind`;
  - `latitude`, `longitude`, `accuracyMeters` (sabit kültür metni), `locationError` denied|unavailable|timeout;
  - `photo` (jpeg / png ≤10 MB), `photoCapturedAt`;
  - `qrPayload` (≤200), `qrScannedAt`;
  - `clientPlatform` (android), `appVersion`, `mockLocationDetected`, `integrityProvider` (play_integrity), `integrityVerdict`, `integrityToken` (≤8 KB, yalnız boyutu saklanır).

  **Sırayla denetlenir:**
  1. Ziyaret kendi ziyaretin olmalı.
  2. Plan aktif olmalı (`visit_not_confirmed`).
  3. Gün bugün olmalı (`visit_not_today`).
  4. Daha önce başlamamış ve raporlanmamış olmalı (`visit_already_started` / `visit_already_reported`).
  5. 48 saat kilidi geçmemiş olmalı (`visit_report_deadline_passed`).
  6. **Konum.**
     - `required` modunda: `visit_location_required` · `visit_location_too_far` (+`distance_meters=`) · `visit_location_inaccurate`.
     - `info` modunda bayrakla geçer.
     - Koordinatsız kurum: `allow_flagged` ya da `visit_account_location_unknown`.
  7. **QR** (`off|optional|required`; uzaktan ziyarette uygulanmaz): `visit_qr_required`, `visit_qr_invalid`, `visit_qr_wrong_account`, `visit_qr_revoked`, `visit_qr_not_active`.
  8. **Cihaz** (`antiFraudMode` off|flag; asla reddetmez). Bayraklar: `mock_location`, `device_integrity_failed`, `device_unverified`.
  9. **Fotoğraf:** `visit_photo_required`, `visit_photo_invalid`.

- **Politika katmanları:**
  - Sıra: kiracı → tüzel kişilik (temsilcinin birincil koltuğu) → iş birimi (birincil bölge ataması).
  - **Süreli istisna:** en çok 30 gün. Gevşetebileceği kurallar: `location_info`, `photo_optional`, `account_without_location_allow`, `qr_optional`.
  - İstisnayla başlatılan ziyaret `started_with_exception` bayrağı alır.
  - Varsayılanlar: konum zorunlu, 300 m, doğruluk 150 m, fotoğraf isteğe bağlı, kısa ziyaret 120 sn.
- `POST …/qr-check` → yazmadan QR denemesi.
- `PUT …/{id}/capture` · `POST …/{id}/finish` → sunum sırasındaki yakalamalar (bölüm 3). Süre sunucu anlarından hesaplanır; `short_visit` bayrağı.
- **`POST …/{id}/signature` (multipart) — doktor onayı:**
  - alanlar: `file` PNG, `confirmed`, `context` approval|sample;
  - **yalnız bitişten sonra** (`visit_execution_not_finished`); ayrıca `visit_signature_exists`.
- **Durumlar:** `in_progress` = başladı / bitmedi. Bitip raporsuz kalan = `report_missing`. Geçmiş gün, yürütme yok = `missed`. Süre doldu = `expired` + `managerAttention`.

---

## 3. Ziyaret sunumu (doktora gösterilen içerik)

**Web'de ne var:** iki panelli ekran.
- **Doktor ekranı:** slayt başlığı, alt başlık, onaylı iddialar, kaynaklar, görsel alanı.
- **Temsilci kumandası:**
  - ürün sayacı, slayt sayacı, Geri / İleri / Ürün atla;
  - sekmeler: **Notlar** (ana mesaj, konuşma metni, bu doktor için kısa özet, **onaylı itiraz yanıtları**, kendi notum), **İşaretler** (İlgili / Nötr / İlgisiz, Soru sordu, İtiraz etti + **kategori**, Numune istedi, Literatür istedi, Takip gerekli), Not al.
- **Yüz yüze / Uzaktan** seçimi; **Ziyareti bitir**.

**Uçlar:**
- `GET api/crm/visit-presentation/{plannedVisitId}?lang=` (`crm.visit-report.read` + sahiplik) → `{slides[], objectionCategories[{code, label}], warnings[]}`.
  - **Slayt:** `index`, `productId`, `productCode`, `productName`, `role`, `stepId`, `contentId`, `title`, `subtitle`, `bullets[]` (yalnız onaylı iddialar), `references[]`, `asset{kind, url}`, `presentation{keyMessage, talkTrack, targetSeconds, transition}`, `objections[{id, categoryCode, categoryLabel, question, answer, references}]` (yalnız onaylı kütüphane yanıtları).
  - Promosyon ürününde tüm adımlar gelir; hatırlatma ürünü ve **eczane ziyaretinde yalnız ilk adım**.
  - Uyarılar: `no_content`, `no_steps`, `path_version_missing`, `content_missing`, `asset_not_available`.
- `GET …/{id}/slides/{stepId}/asset` → **şimdilik her zaman 404** (`presentation_asset_unavailable`). Slayt görselleri henüz sunulmuyor.

**Yakalanan veriler** (capture / finish gövdesi):
- `kind` in_person|remote;
- `productTimings[{productId, seconds}]`, `slideTimings[{productId, stepId, seconds}]`;
- `marks[{productId, interest interested|neutral|not_interested, flags[question|objection|sample_request|literature_request|follow_up], objectionCategory}]`;
- `note` ≤2000;
- `personalNotes[{productId, text ≤1000}]`: yalnız yazara görünür, rapora gitmez;
- `coVisitorUserIds` ≤3.

---

## 4. Ziyaret raporu V2 — hekim ve **eczane**

**Web'de ne var:** 5 adımlı sihirbaz.
- Hekim raporu: Genel → her ürün için A / B / C → Takip ve güvenlik → Doktor profili → Özet ve gönder.
- Üstte "Rapor için NN sa kaldı", eksik alan sayacı ve otomatik taslak.
- Sunumdan gelenler önceden dolu: sunulan içerik ve slayt süreleri ("plana uydu"), ürün süresi, ilgi, itiraz satırları.
- Eczane raporu ayrı biçimde (aşağıda).

**Uçlar** (`crm.visit-report.read` · `.record` + `crm.planned-visit.manage` · `.amend`):
- `GET api/crm/visit-report/v2/prefill?plannedVisitId`:
  - `reportKind` doctor|pharmacy, `body` (yürütmeden dolu), `previous`, `profile`;
  - `potentials[]` (isDue), `indicationRows[]`;
  - `competitors[{productId, available, items}]`;
  - `optionSets{set → [{code, label}]}`.
- `PUT …/v2/draft` (`If-Match` / `expectedVersion`).
- `POST …/v2/submit` → `{visitReportId, version, reportStatus, missing[{section, productId, field}], warnings[]}`:
  - hatalar: `report_incomplete` (+`missing[]`), `report_v2_invalid`, `visit_report_deadline_passed`, `visit_report_edit_window_closed`, `visit_report_concurrency_conflict`;
  - **60 dakika** içinde aynı rapor yeniden gönderilebilir, sonra yalnız **düzeltme (amend)**.
- `POST …/{visitReportId}/amend` `{reason ≤500, v2:{…}}`: ekleme yapılır, eski değerler korunur.
- `POST / GET …/v2/{plannedVisitId}/shelf-photo` → **eczane raf fotoğrafı** (JPEG / PNG ≤10 MB) → `{photoEvidenceId}`.

**Gövde bölümleri:**
- **general:** `visitKindCode`, `coVisitorUserIds`, `metContactId` (hekim) / **`metRoleCode` (eczane)**, `generalNote` ≤4000.
- **productSections[]** (yalnız hekim):
  - `minutes` 1–240;
  - `visitSuccessCode`, `commitmentCode`, `interestCode`, `adoptionStageCode` (farkında değil → savunucu; içerik yolculuğunu ilerletir);
  - `potential{rows[{indicationNodeId, profileNodeId, monthlyPatients}], noPatientsInIndications}` (90 günde bir ve ürünün onaylı endikasyonu varsa);
  - `shareBucketCode`;
  - `competitors[{competitorProductId | proposed+nameSnapshot+company, advantageCodes, disadvantageCodes}]` / `noCompetitor`;
  - `objections[{categoryCode, objectionResponseId, note, resolved yes|partly|no}]` / `noObjection`;
  - `requests[{typeCode, dueDate, note}]` / `noRequest`;
  - `sample{gskuId, quantity 1–100, lot}` / `noSample`;
  - `nextMessage` ≤500.
- **doctor:** `adoptionTypologyCode`, `communicationStyleCode`, `kolLevelCode`, `totalMonthlyPatients` (zamanı gelmişse zorunlu).
- **pharmacy (eczane ziyareti):**
  - `stock[{gskuId, levelCode, nearExpiry, returnIssue}]`;
  - `shelf{positionCode, materialStand, photoEvidenceId}`;
  - `sales[{gskuId, trendCode, boxesPerMonth}]`;
  - `competitors[]` / `noCompetitor`;
  - `recommendation{levelCode, linkedContactIds}`;
  - `training{given, requests[]}`.
  - Eczane rakip ürünü sırasıyla: planın ilk ürünü → ilk stok GSKU'sunun ürünü → ilk satış GSKU'sunun ürünü.
  - **GSKU araması MDM'den:** `GET api/gskus?search=`.
  - **Yeni eklenen GSKU'nun rakipleri:** MDM `GET api/mdm/competitor-products/lookup?productId=` (`mdm.competitor-products.read`).
- **safety:** `adverseEvent` (zorunlu) + `adverseEventNote` (Evet ise zorunlu); `offLabelRequest` (yalnız hekim; **eczanede sorulmaz**, null zorlanır).
- **followUp:** `required`, `suggestedDate`, `note`.

**Süre:** planlanan günün sonu (UTC) + **48 saat**. Öncesinde `report_missing`, sonrasında `expired` ve rapor kilitli.

---

## 5. Ziyaret bildirimleri (e-posta)

(`crm.visit-notifications.read` · `.settings.manage`)

**Ne oluyor:** rapor gönderilince Platform üzerinden e-posta gider. Hiçbiri rapor akışını bozmaz; en çok 5 yeniden deneme.

**Olaylar:**
- `crm.visitreport.submitted` → yönetici (Reports-to zinciri).
- `crm.visitreport.adverseevent` → **PV posta kutusu** + yönetici bilgi. Konu "ACİL · … · 24 saat".
- `crm.visitreport.offlabelrequest` → **MSL** + yönetici bilgi (yalnız hekim).
- `crm.visitreport.overdue` → yönetici (zamanlayıcı).

**Uçlar:**
- `GET api/crm/visit-notifications` (`from`, `to`, `eventCode`, `status`, `resourceId`, `page`) → `{items[{id, eventCode, status queued|sent|failed|no-recipient|route-pending, plannedVisitId, visitReportId, resourceName, visitDate, createdAt, sentAt, attempts}]}`.
- `GET …/{id}` → alıcı rolleri, değişkenler. **E-posta adresi asla dönmez.**
- `GET / PUT …/settings` → `{pvEmail, mslEmail, overdueReminders, reportSummary, version}`.

**Görünürlük:** temsilci kendi, yönetici ekibi, ayar yetkilisi hepsini görür.

**PV kararları (2026-10-21, uygulanacak — W5-AE paketi):**
- Advers olay **rapordan bağımsız** ayrı bir "Advers olay bildir" eylemiyle anında gönderilecek.
- **Farkına varma anı** zorunlu olacak; 24 saat bu andan sayılır.
- **Şüpheli ürün zorunlu** ve ürün kaydına bağlı olacak.
- CRM advers notunu **saklamayacak**.
- **Mobil de buna göre değişecek;** sözleşme eki ayrıca gelecek.

---

## 6. Eczane ön siparişi (MOD-0168)

**Web'de ne var:**
- **Sipariş formu (temsilci):**
  - üstte "Sistemden" şeridi: satan şirket, ülke, para birimi, fiyat modu, geçerli fiyat listesi;
  - eczane seçimi;
  - ürün satırları (SKU, adet, MF a+b, iskonto %), satır altında birim fiyat (kilitli), birleşik indirim, fiyat kaynağı ve kural rozeti (Sınır içi / Yumuşak / Sert);
  - eczanenin önceki siparişleri;
  - teslimat: depo / distribütör, teslim tarihi, para birimi, depoya not;
  - sipariş özeti: Ara toplam (liste), İskonto, Net, KDV, Etkin indirim, Genel toplam;
  - "Onay gerekir mi?" kutusu: onaycının adı ve pozisyonu;
  - **eczacı imzası** (ad + çizim) ve Gönder.
- **Liste** (süzgeçler: dönem, durum, temsilci, eczane, depo, ürün, ülke, "Onayımı bekleyenler").
- **Onay ekranı (yönetici):**
  - "Neden onayımda?";
  - satırlarda girilen değer ve kural sınırı;
  - eczanenin son 6 ayı;
  - Onayla / Reddet / Değişiklik iste (son ikisinde gerekçe zorunlu).
- **Depo belgesi (PDF):**
  - depo nüshası ve eczane kopyası;
  - antette şirket adresi ve VKN;
  - satırlar: Barkod / Adet / MF / İsk. % / Birim fiyat / KDV / Satır toplamı;
  - imza, onaylayan, yerel saat;
  - "faturayı depo düzenler" notu.

**Uçlar** (`commercial.pre-orders.read` · `.create` · `.submit` · `.cancel`; temsilciye ayrıca `platform.workflow.instances.start` / `.view`):

| Uç | Ne yapar |
|---|---|
| `GET api/commercial/pre-orders/context` | `{legalEntityId/Code/Name, companyPrefix, countryCode, localCurrencyCode, priceMode priced\|quantity, currency, priceList{id, version, name, kind, validFrom}, tier, approvalMode, approver{available, positionCode, positionName, managers[]}}` |
| `POST …/preview` | `{pharmacyAccountId, lines[]}` → satır başına `unitPrice`, `vatRate`, `netAmount`, `freeQuantity`, `mfRatioPct`, `combinedDiscountPct`, `outcome ok\|soft\|hard`, `findings[{code, severity, params}]`; `totals{net, vat, gross, effectiveDiscountPct, listSubtotal, discountAmount}`; `approvalRequired`. **İstemcide fiyat / indirim hesabı yapılmaz; hepsi buradan.** |
| `POST …` (taslak) | `{pharmacyAccountId, depotAccountId?, requestedDeliveryDate, note, visitId, lines[{skuId, quantity, mf{buy, free}, discountPct}]}` → 201 `PreOrderDto`. Numara taslakta verilir: `{PREFIX}-ORD-000001` |
| `GET / PUT …/{id}` | Yalnız sahip, yalnız taslak (`expectedVersion`) |
| `POST …/{id}/signature` | JSON `{signerName ≤200, imageBase64, mediaType image/png\|image/jpeg, expectedVersion}`. **Satır ya da eczane değişince imza silinir** |
| `POST …/{id}/submit` | `{expectedVersion}`. Ret nedenleri: `pre_order_depot_required`, `pre_order_signature_required`, `pre_order_depot_no_email`, `pre_order_hard_limit` (+`L{n}:{kod}`), `pre_order_no_approver`, `pre_order_workflow_unavailable` (503) |
| `POST …/{id}/decision` | Yönetici: `{decision approve\|reject\|request-changes, reason, expectedVersion}`. `pre_order_sod` (kendi siparişine karar veremez) |
| `POST …/{id}/cancel` | `{reason (zorunlu), expectedVersion}`; yalnız onaylı / iletilmiş / iletimi başarısız siparişte; **bekleyen sipariş iptal edilemez** |
| `POST …/{id}/copy` | Reddedilen / iptal edilenden yeni taslak |
| `POST …/{id}/discard` | Taslağı sil |
| `GET …/{id}/document?copy=depot\|pharmacy` | PDF |
| `GET …/{id}/pharmacy-history` | Eczanenin son 6 ayı (temsilciye büyük ihtimalle 404; yalnız onaycı / read-all) |
| `GET api/commercial/pre-orders` | Liste (`status`, `from`, `to`, `pharmacyAccountId`, `depotAccountId`, `skuId`, `awaitingMe`, `search`, `page`) |

**Seçim listeleri (dikkat):** CRM'de ayrı uç **yok**. Web kendi proxy'sinde şu uçları birleştiriyor:
- eczane: `GET api/crm/accounts?accountType=pharmacy&search=`;
- depo: aynı uç, `accountType=distributor` ve `wholesaler`;
- ürün: MDM `GET api/product-master/stockable-items?status=Active&q=` (`mdm.product-master.read`; `skuLevel=Item` hariç).

Mobil de aynısını yapmalı ya da bizden uç istemeli.

**Durumlar:**
- draft → pending_approval → approved → transmitted;
- pending_approval'dan: rejected, ya da request-changes ile draft'a dönüş (imza silinir, olay geçmişi korunur);
- iletim başarısız olursa transmission_failed (otomatik yeniden deneme);
- approved / transmitted siparişten cancelled.
- **Onay modu** (kiracı ayarı): `always` → her sipariş yöneticiye; `auto-within-limits` → yalnız yumuşak sınır aşımında.
- **Onaycı:** temsilcinin birincil koltuğunun Reports-to zinciri.

**Doğrulama kuralları:**
- eczane tipi `pharmacy`, depo tipi `distributor|wholesaler` olmalı;
- teslim tarihi bugün ya da sonrası;
- not ≤1000 karakter;
- ≤50 satır ve her SKU bir kez;
- MF a,b ≥1 (`pre_order_mf_invalid`);
- iskonto 0 ≤ x < 100, bir ondalık;
- **adetli modda iskonto yok** (`pre_order_discount_not_allowed`);
- yalnız Active SKU (`pre_order_sku_retired`).

**Fiyat kural kodları:** `discount|mf|combined.soft-/hard-exceeded`, `quantity.below-min`, `quantity.soft-/hard-exceeded`, `price.sku-not-in-list`.

**Yanıttaki `can{edit, submit, decide, cancel, copy}`** düğmeleri açıp kapatmak içindir; istemci kural yazmaz.

---

## 7. Müşteri (kurum) güncelleme ve diğer

- `PUT api/crm/accounts/{id}` (`crm.account.update`):
  - **`expectedVersion` zorunlu**;
  - logo gönderilmezse korunur, `removeLogo` siler, ikisi birden `account_logo_conflict`;
  - yanıt `data:true` (yeni sürüm dönmez → yeniden GET);
  - hatalar `account_*_invalid`, `account_version_conflict`.
- `GET api/crm/resources/me` → `{items[{resourceId, displayName, countryCode}]}` (izin anahtarı gerekmez).
- Planlı ziyaret yetkileri: `crm.planned-visit.read` / `.manage` / `.confirm` / `.read-all` (eski bölge yedeği kaldırıldı).

---

## 8. Temsilci rolünün izin anahtarları (tam liste)

- **Planlama:** `crm.visit-plan.read` · `.generate` · `.apply`; `crm.planned-visit.read` · `.manage` (· `.confirm`).
- **Yürütme, sunum, rapor:** `crm.visit-execution.read` · `.record`; `crm.visit-report.read` · `.record` · `.amend`.
- **Bildirim listesi:** `crm.visit-notifications.read`.
- **Ana veri:** `crm.account.read` (+ `crm.account.update`), `crm.contact.read`, `crm.account-contact.read`, `crm.account-relationship.read`; MDM `mdm.global-products.read`, `mdm.product-master.read`, `mdm.competitor-products.read`.
- **Ön sipariş:** `commercial.pre-orders.read` · `.create` · `.submit` · `.cancel`; `platform.workflow.instances.start` · `.view`.
- **Geçici:** `crm.territory.read`.
- **Temsilciye VERİLMEZ:**
  - `crm.visit-evidence-policy.*`, `crm.visit-evidence-exception.*`, `crm.visit-qr.*`;
  - `commercial.pre-orders.approve` / `read-all` / `record-depot-response` / `retry-transmission`;
  - fiyat ve ayar anahtarları.

---

## 9. Canlı örnek akış (yerel test, kiracı 97c5, 2026-10-23) — karşılaştırmada referans olarak kullan

1. **Planlama:** 43. hafta "Taslak · otomatik" durumunda.
   - Kapasite 38,3 sa, planlı 7,6 sa; 31 ziyaret yerleşti, 23 hedef sığmadı.
   - `POST …/apply` → `scheduledCount:31, unscheduledCount:23, weekStatus:"approved"`.
2. **Bugünün ziyareti:** `plannedVisitId 7ff6bcd8-a4bc-495e-865a-c1d1f1f5ec22` · **TARIK ESEN** (Üroloji) · AMERİKAN HASTANESİ (Şişli) · 09:00–09:06 · 6 dk.
   - `plannedContent`: TUTUKON (`GP-000000000063`) · tanıtım · yolculuk `CEJ-2026-8AD806` · aşama **"Farkındalık"** · 3 içerik adımı.
   - Sıklık: dönemde 14 ziyaret, şimdiye kadar 0/13.
3. **Başlatma:** konum kuralı "Bilgi". Tarayıcı konumu reddetti → bayrakla başladı. Fotoğraf isteğe bağlı. `executionId 347eafd1-a963-4abd-9eb5-ec253d707963`, başlangıç 11:14.
4. **Sunum:** `slides` = 3. TUTUKON "Sindirim konforu (detaylama)" ×2 ve "Kullanım ve dozaj".
   - `warnings:["asset_not_available"]`.
   - İşaret: İtiraz etti · kategori **Fiyat**.
   - Süreler: 82 sn / 13 sn / 75 sn.
5. **Bitiş:** 11:17, süre 02:50. Doktor imzası (`context: approval`, `confirmed: true`) alındı.
6. **Rapor (hekim):**
   - Yüz yüze; TUTUKON: Ulaşıldı · Deneyecek · İlgili · Değerlendiriyor;
   - reçete payı "Ara sıra"; rakip kullanmıyor;
   - itiraz Fiyat · Kısmen; talep yok; numune verilmedi;
   - **advers olay: Evet** (not girildi); endikasyon dışı: Hayır;
   - doktor profili: Erken benimseyen · Analitik · Yerel KOL · aylık 120 hasta;
   - gönderildi (60 dk düzeltme penceresi).
7. **Bildirimler:**
   - "Advers olay bildirimi" → **PV güvenlik + Yönetici (bilgi) · İletildi**.
   - Önizleme: "ACİL · Advers olay bildirimi · TARIK ESEN · 24 saat"; şüpheli ürün; temsilci notu; "Temsilci tıbbi değerlendirme yapmaz"; KVKK notu.
   - "Rapor özeti" → Alıcı yok (bu kullanıcı CEO koltuğunda; yöneticisi yok).
8. **Ön sipariş `MG-ORD-000002`:**
   - Taraflar: eczane 1 EYLÜL ECZANESİ · depo TEST ECZA DEPOSU (wholesaler).
   - Satır: 10 × KONSET · 28 C62 · birim 110,00 TRY (KONSET TR test listesi, eczacı alış kademesi) · KDV %10 · toplam 1.210,00 TRY.
   - Akış: gönderildi → yönetici **değişiklik istedi** (gerekçe: "Adet 10 olsun…"; taslağa döndü, imza silindi) → düzeltildi, yeniden imzalandı (`submissionNo:2`) → onaylandı → **transmitted** (depo PDF'i + e-posta, dil tr).

---

## 10. Bilinen eksikler (karşılaştırmada "bizde de yok" diye işaretle)

- Slayt görselleri sunulmuyor (`asset` 404).
- Takvimdeki `plannedContent` ürün **adı** taşımıyor, yalnız kod. Sunum yanıtında `productName` var.
- E-postadaki onay bağlantısıyla giriş kiracıyı kaybediyor (düzeltiliyor, W6-FIX-a).
- Onay isteği e-postası gitmiyor (düzeltiliyor, W6-FIX-a).
- Ön sipariş seçim listeleri (eczane / depo / ürün) için CRM'de ayrı uç yok.
- Eczanenin sipariş geçmişi temsilciye kapalı olabilir (?).
- Advers olay akışı PV kararlarıyla yeniden yazılacak (W5-AE).
