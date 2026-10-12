# WORK PACKAGE — WP-VW-W3 · Ziyaret yürütme: kanıt, sunum, bitiş — 2026-10-12

> **Kaynaklar:**
> - [Yol haritası](ROADMAP-visit-workspace.md) W3 satırı ve K-W2 / K-W3 kararları.
> - Mockup: [`mockups/visit-workspace/decoded/02-ziyaret-kanit-sunum.html`](mockups/visit-workspace/decoded/02-ziyaret-kanit-sunum.html) (E3, E4a, E4b, E4c); `00-demo-veri.js`, `00-tema.css`, `BRIEF-visit-workspace-calendar.md`. Birleşik ekran seçici: `Ziyaret Calisma Alani (standalone).html` → "E3 · …", "E4 · …".
> - Önceki paketler: [W1](WP-VW-W1-visit-report-rules-statuses.md), [W2](WP-VW-W2-calendar-workspace.md) + [2. tur](WP-VW-W2-round2.md) + [3. tur](WP-VW-W2-round3.md).
>
> **Taban:** test dalı `test/crm-content-visit-e2e` (CRM 2558/0/5, Web 867/0, mimari 27).

## 0. Kararlar

**Kullanıcı:**
- **K-W2:** kanıt kuralları **kiracı ayarı**; varsayılan **konum zorunlu + fotoğraf isteğe bağlı**. Mockup'taki "fotoğraf zorunlu" varsayılanı yerine K-W2 geçerli.
- **K-W3 (2026-10-12):** fotoğraf ve imza **MOD-0262-FU01 belge deposunda** (`ContentStorageScope.CrmVisitEvidence`).
- **İzin (2026-10-12):**
  - Temsilciye kiracı geneli `platform.document-repository.read/manage` **verilmez**.
  - Platform'a yalnız ziyaret kanıtı kapsamını açan izinler eklenir.
  - Temsilci kanıtı yalnız kendi ziyareti üzerinden, CRM aracılığıyla görür.
- **Faz 8 en sonda:** yeni CRM yazma komutları mimari testin denetimsiz sayısını artırır; §37 yeni sayıyı yazar.

**CT (bu paketin varsayılanları; kullanıcı değiştirebilir):**
1. **Mesafe eşiği:** kiracı ayarında `radiusMeters` (varsayılan **300 m**), `maxAccuracyMeters` (varsayılan **150 m**).
2. **Masaüstü muafiyeti YOK.** Cihaz türü istemcinin beyanıdır, sunucu güvenemez. Konum "zorunlu" ise her cihazda konum gerekir; kiracı "bilgi" seçerse kaydedilir ve işaretlenir.
3. **Konumu olmayan kurum** (Account'ta enlem/boylam yok): `accountWithoutLocation = allow_flagged` (varsayılan; başlatılır, `account_location_unknown` işareti, yönetici görür) | `block`.
4. **QR ve anti-fraud W3'te YOK.** Kurum QR kodu verisi ve mobil anti-fraud modülü yok. Ayar alanları da eklenmez; yol haritasında **W3E · Ek kanıtlar** (W4'ten sonra, öneri).
5. **Kiracı ayar ekranı 2. turda YOK.** Varsayılanlar geçerli; ayar API ile değişir. Yönetim ekranı **W3-WEB-d** (mockup yaptırılıyor, kullanıcı 2026-10-12).
6. **PIN:** temsilci doktor moduna geçerken 4 haneli PIN belirler. PIN yalnız tarayıcı belleğinde (özet) tutulur, sunucuya gitmez. PIN ile çıkış **ziyareti bitirmez**, temsilci panelini açar. Bitirme, panelde ayrı bir düğmedir (mockup boşluğu kapatılır).
7. **"Bu doktor için"** W3'te mevcut rapor verisinden basit özet: son ziyaret tarihi, sonuç, takip notu, verilen numune. Bağlılık, ilgi eğilimi ve iletişim stili W4.
8. **Slayt görseli:** içerik görseli temsilciye yeni Platform izni vermeden okunabiliyorsa gösterilir; okunamıyorsa yer tutucu ("onaylı içerik görseli"), follow-up.
9. **Rakip kartı ve pazar payı** W3'te YOK (K-W6 / W7). İtiraz yanıtı yalnız onaylı metin ve iddia referanslarıdır.
10. **JourneyProgress ilerletme (SB-3c)** W3'te YOK. Rapor gönderimine bağlanır (W4).
11. **İtiraz kütüphanesi ve sunum notu yazım ekranı** (Content Studio) bu turlarda YOK → **W3-WEB-c**. Kullanıcı (2026-10-12): **mockup yaptıracak**; brief [BRIEF-W3-authoring-and-evidence-settings](mockups/visit-workspace/BRIEF-W3-authoring-and-evidence-settings.md).

13. **Kanıt ayarı katmanları (kullanıcı 2026-10-12):** en özel olan kazanır → **kiracı varsayılanı** < **tüzel kişilik** < **iş birimi**; alan bazında miras (katman yalnız değiştirdiği alanı tutar). Temsilci için kalıcı ayar YOK; **süreli istisna** (gevşetilebilen: konum → bilgi, fotoğraf → isteğe bağlı, konumsuz kurum → başlat; en çok 30 gün; gerekçe; SoD onay). Bugünkü BE-a yalnız kiracı katmanı; katmanlar + istisna backend'i **W3-WEB-d ile birlikte** (W3-BE-c). Ön koşul: temsilcinin tüzel kişilik / iş birimi bağının güvenilir kaynağı doğrulanacak (HR kimlik açığı). Brief §C güncellendi.
12. **İtiraz yanıtı onayı (kullanıcı 2026-10-12):** şimdilik **basit onay** (yazan onaylayamaz, SoD) + yalnız onaylı iddialara bağlanma. **Takip (FU-W3-OBJ-MLR):** ileride yanıt metni Claims'in MLR inceleme akışına bağlanacak (tıbbi / hukuki / mevzuat kontrolü). BE-b, onay geçişini tek bir noktada (komut + durum makinesi) tutsun ki sonra değiştirilebilsin.

**Kapsam dışı:** rapor ekranı (W4), e-postalar (W5), ön sipariş (W6), satış payı (W7), çevrimdışı kuyruk (mobil uygulamanın işi).

## 1. Tur planı
| Tur | Prompt | Çalışma yeri / dal | Bağımlılık |
|---|---|---|---|
| 1 | **W3-PLT** · Platform: kanıt kapsamı + kapsamla sınırlı izin | `C:\tmp\vw-w3-plt` / `wp/vw-w3-plt` | — |
| 1 | **W3-BE-a** · CRM: ziyaret yürütme kaydı + kiracı kanıt ayarı | `C:\tmp\vw-w3-be-a` / `wp/vw-w3-be-a` | Platform adları §2.1'de sabit |
| 1 | **W3-BE-b** · CRM: sunum notları + itiraz kütüphanesi + ziyaret sunum okuması | `C:\tmp\vw-w3-be-b` / `wp/vw-w3-be-b` | — |
| 2 | **W3-WEB-a** · Çalışma alanı: başlat (E3), `in_progress`, eylemler | `C:\tmp\vw-w3-web-a` / `wp/vw-w3-web-a` | 1. tur test dalında |
| 2 | **W3-WEB-b** · Sunum sayfası: doktor ekranı (E4a), kumanda (E4b), bitiş + imza (E4c) | `C:\tmp\vw-w3-web-b` / `wp/vw-w3-web-b` | 1. tur test dalında |
| 3 | W3-WEB-c · Content Studio yazım ekranları (itiraz kütüphanesi + adım sunum notları) | — | mockup (brief hazır) + BE-b |
| 3 | W3-WEB-d · Ziyaret kanıtı ayarları ekranı | — | mockup (aynı brief §C) + BE-a |
| CT | W3 mobil notu | — | 1. tur |

1. turun üç promptu paraleldir. Ortak adlar §2'de **sabittir**; ajan değiştirmez, değiştirmek zorunda kalırsa DURUR ve raporlar.

## 2. Sabit sözleşme

### 2.1 Platform (W3-PLT)
- **Kapsam:**
  - `ContentStorageScope.CrmVisitEvidence = 4`;
  - nesne anahtarı klasörü `crm-visit-evidence` (`BuildObjectKey` kendi kolu; varsayılan `_ => "documents"` kolu yeni kapsamı yutmamalı).
- **Kapsam kuralları:**
  - yalnız `.jpg`, `.jpeg`, `.png` (`image/jpeg`, `image/png`);
  - en çok **10 MB** (yapılandırma `ContentStorage:Scopes:CrmVisitEvidence:MaxFileSizeBytes`, varsayılan 10485760).
- **İzinler** (açıklamalar yalnız İngilizce):
  - `platform.document-repository.visit-evidence.write`: `CrmVisitEvidence` kapsamına yazar; yalnız **kendi oluşturduğu** kanıtı telafi silmesiyle kaldırır.
  - `platform.document-repository.visit-evidence.read`: yalnız **kendi oluşturduğu** `CrmVisitEvidence` nesnesinin üst verisini ve içeriğini okur.
  - `platform.document-repository.visit-evidence.read-all`: kiracıdaki tüm `CrmVisitEvidence` nesnelerini okur (yönetici).
- **Kurallar:**
  - Kapsamlı anahtarlar başka kapsamı **açmaz**. Örneğin `visit-evidence.write` ile `Documents` kapsamına yazma 403 döner.
  - Genel `.read` / `.manage` davranışı **değişmez** (geriye uyum).
  - Kapsamlı okumada yetki yoksa (başkasının kanıtı, başka kapsam) **404** döner, 403 değil. Bu, kiracılar arası gizlemeyle aynıdır.
  - Yetkilendirme servis sınırında yapılır (AD-5). Denetleyicideki öznitelik "genel anahtar VEYA kapsamlı anahtar" kabul eder; kapsam ve sahip kontrolü serviste yapılır.
- **Seed / grant YOK.** Anahtarlar Platform'un izin kataloğunda tanımlanır. Temsilci ve yönetici rollerine verme, kullanıcının çalıştıracağı betikle yapılır (CT hazırlar).

### 2.2 CRM yürütme (W3-BE-a)
**İzinler:**
- `crm.visit-execution.read`, `crm.visit-execution.record` (temsilci);
- `crm.visit-evidence-policy.manage` (yönetici);
- başkasının ziyareti için mevcut `crm.planned-visit.read-all` kullanılır.
- Anahtarlar Platform `CrmManifestProvider`'da tanımlanır. Seed / grant yok.

**Kiracı ayarı:** `VisitEvidencePolicy`, kiracı başına tek belge.

| alan | tür | varsayılan | sınır |
|---|---|---|---|
| `locationMode` | `required` \| `info` | `required` | |
| `radiusMeters` | int | 300 | 50–5000 |
| `maxAccuracyMeters` | int | 150 | 10–1000 |
| `photoMode` | `required` \| `optional` | `optional` | |
| `doctorConfirmation` | bool | true | |
| `durationConsistency` | bool | true | |
| `shortVisitSeconds` | int | 120 | 30–1800 |
| `accountWithoutLocation` | `allow_flagged` \| `block` | `allow_flagged` | |

- Belge yoksa varsayılanlar geçerlidir; okumada belge **yazılmaz**.
- `GET` / `PUT api/crm/visit-evidence-policy` (`crm.visit-evidence-policy.manage`).

**Uç noktalar** (`api/crm/visit-execution`):

| yöntem / yol | gövde | izin |
|---|---|---|
| `GET contract` | — | `.read` |
| `GET by-plan/{plannedVisitId}` | — | `.read` + sahiplik |
| `GET by-plan/{plannedVisitId}/brief` | — | `.read` + sahiplik |
| `POST start` | multipart: `plannedVisitId`, `kind`, `latitude?`, `longitude?`, `accuracyMeters?`, `locationError?` (`denied`\|`unavailable`\|`timeout`), `photo?` (dosya), `photoCapturedAt?` | `.record` + sahiplik |
| `PUT {id}/capture` | JSON yakalama (aşağıda) | `.record` + sahiplik |
| `POST {id}/finish` | JSON yakalama | `.record` + sahiplik |
| `POST {id}/signature` | multipart: `file` (png), `confirmed` (bool), `context` (`approval`\|`sample`) | `.record` + sahiplik |
| `GET {id}/photo` · `GET {id}/signature` | — (akış) | `.read` + sahiplik (veya `planned-visit.read-all`) |

**Yakalama** (`capture` / `finish` gövdesi):
```json
{ "kind": "in_person|remote",
  "productTimings": [{ "productId": "…", "seconds": 0 }],
  "slideTimings":   [{ "productId": "…", "stepId": "…", "seconds": 0 }],
  "marks": [{ "productId": "…", "interest": "interested|neutral|not_interested|null",
              "flags": ["question","objection","sample_request","literature_request","follow_up"],
              "objectionCategory": "<visit-objection-category kodu|null>" }],
  "note": "…", "personalNotes": [{ "productId": "…", "text": "…" }],
  "coVisitorUserIds": ["…"] }
```

**`VisitExecutionDto`:**
- `id`, `plannedVisitId`, `resourceId`, `status` (`started`|`finished`), `kind`;
- `startedAt`, `finishedAt`, `durationSeconds`;
- `location`: `{ latitude, longitude, accuracyMeters, distanceMeters, radiusMeters, mode, status: ok|far|missing|inaccurate|account_unknown }`;
- `photo`: `{ present, mediaType, byteSize, capturedAt }`;
- `signature`: `{ present, confirmed, context, signedAt }`;
- `productTimings`, `slideTimings`, `marks`, `note`;
- `personalNotes` (**yalnız sahibine**; başkasına boş);
- `coVisitors[{ userId, displayName }]`;
- `shortVisit`;
- `flags[]` (`location_far`, `location_missing`, `location_inaccurate`, `account_location_unknown`, `short_visit`);
- `reportDeadline`.

**contentId ve nesne anahtarı istemciye ASLA dönmez.**

**`contract`:**
- `moduleId: "MOD-0155-VX"`, `policy` (etkin ayar), `visitKinds`, `interestValues`, `flagCodes`;
- `objectionCategorySet: "visit-objection-category"`;
- `maxNoteLength` (2000), `maxPersonalNoteLength` (1000), `maxCoVisitors` (3);
- `errorCodes`, `permissions`.

**Hata kodları:**
- 404: yabancı plan veya yürütme.
- 409:
  - `visit_not_today`, `visit_not_confirmed`, `visit_already_started`, `visit_already_reported`;
  - `visit_location_required`, `visit_location_too_far`, `visit_location_inaccurate`, `visit_account_location_unknown`;
  - `visit_execution_finished`, `visit_execution_not_finished`, `visit_signature_exists`, `visit_report_deadline_passed`.
- 400: `visit_photo_required`, `visit_photo_invalid`, `visit_capture_invalid`, `visit_co_visitor_invalid`.
- 503: `visit_evidence_store_unavailable`.

**Durum (`workStatus`):**
- `in_progress` eklenir, `today`'den **önce**: yürütme `started` ve `finished` değil.
- Bitince mevcut kurallar (`today` / `report_missing` …) geçerlidir.
- Takvim öğesine eklenenler: `executionId`, `executionStatus`, `startedAt`, `finishedAt`.
- `plannedContent[].steps[]` öğesine `minutes` ve `contentId` eklenir (W2 Web zaten `minutes`'i bekliyor).

**Rapor bağı (W1 / W2 komutları):**
- Yürütme başlamışsa `missed` / `rescheduled` sonucu ve plan iptali **409** `visit_execution_started` döner.
- Başlamış ama bitmemişse rapor gönderimi **409** `visit_execution_not_finished` döner.
- Bitmiş yürütme varsa raporun `ExecutedAt` değeri **sunucudaki `startedAt`** olur; istemci değeri yok sayılır.

### 2.3 CRM içerik (W3-BE-b)
**Sunum notları:** `KnowledgePathStep.presentation`:

| alan | sınır |
|---|---|
| `keyMessage` | ≤ 500 |
| `talkTrack` | ≤ 4000 |
| `targetSeconds` | 10–3600 \| null |
| `transition` | ≤ 500 |
| `objectionResponseIds[]` | ≤ 10 |

- Yol sürümüyle birlikte gelir: taslakta düzenlenir, yayınlanınca donar (mevcut dondurma kuralı).

**İtiraz kütüphanesi:** yeni toplam `ObjectionResponse`.
- Alanlar:
  - `productId` (MDM referansı);
  - `categoryCode` (BRD seti `visit-objection-category`);
  - `question` (≤ 500), `answer` (≤ 4000);
  - `claimIds[]` (yalnız **onaylı** iddialar);
  - `status` (`draft` → `approved` → `retired`);
  - `approvedBy` / `approvedAt`.
- Onaylayan yazar olamaz (SoD). Onaylı kayıt değişmez; değişiklik için `retired` + yeni kayıt.
- İzinler: `crm.objection-response.read | manage | approve`.
- Uç noktalar: `api/crm/objection-responses` (liste / al / oluştur / güncelle / onayla / emekliye ayır).

**BRD seti `visit-objection-category`** (kiracı, Platform katalog yükleyicisi; `visit-outcome-reason` deseni):
- `price`, `reimbursement`, `efficacy`, `side_effect`, `dosage`, `access_stock`, `habit_competitor`, `insufficient_evidence`, `other`;
- 7 dil etiketi.

**Ziyaret sunum okuması:** `GET api/crm/visit-presentation/{plannedVisitId}?lang=`
- İzin: mevcut `crm.visit-report.read` + sahiplik (`ICallerScope`); yeni anahtar yok.
- **Slayt seçimi:**
  - `ContentItems` `Order` sırasıyla;
  - tanıtım ürününde dondurulmuş tüm adımlar;
  - hatırlatma ürününde yalnız ilk adım;
  - eczane hedefinde her ürünün ilk adımı.
  - Adım alanları planın dondurduğu yol sürümünden (`PathId` + `PathVersion`) okunur.
- **Yanıt:**
```json
{ "plannedVisitId": "…",
  "slides": [{ "index": 0, "productId": "…", "productCode": "…", "productName": "…", "role": "promo|non-promo",
               "stepId": "…", "contentId": "…", "title": "…", "subtitle": "…",
               "bullets": ["onaylı iddia metni …"], "references": ["…"],
               "asset": { "kind": "image|none", "url": "…|null" },
               "presentation": { "keyMessage": "…", "talkTrack": "…", "targetSeconds": 60, "transition": "…" },
               "objections": [{ "id": "…", "categoryCode": "…", "categoryLabel": "…", "question": "…", "answer": "…", "references": ["…"] }] }],
  "objectionCategories": [{ "code": "…", "label": "…" }],
  "warnings": ["no_content", "path_version_missing", "…"] }
```
- `targetSeconds` yoksa adımın `EstimatedDurationMinutes × 60` değeri, o da yoksa `null`.
- `asset.url`, CRM vekil adresidir: `GET api/crm/visit-presentation/{plannedVisitId}/slides/{stepId}/asset`. Okunamıyorsa `kind: none` döner (karar 8).

### 2.4 Web (2. tur)
- **Sunum sayfası:**
  - `/CRM/VisitPresentation?plannedVisitId={id}`;
  - bitiş özeti `&phase=summary`;
  - Web denetleyicisi `VisitPresentationController`, resx `Resources/Views/CRM/VisitPresentation/*`.
- **Çalışma alanı** (WEB-a) bu adrese bağlanır. Sunum sayfası (WEB-b) dönüşte `/CRM/VisitWorkspace` adresini açar.
- **Çakışma önleme:**
  - WEB-a yalnız `VisitWorkspace` dosyalarına dokunur;
  - WEB-b yalnız yeni `VisitPresentation` dosyalarına dokunur;
  - ortak dosya yok. Durum stili gerekirse WEB-b kendi CSS'inde tanımlar.

---

## W3-PLT — Platform kanıt kapsamı (Platform)
1. `ContentStorageScope.CrmVisitEvidence = 4` + `BuildObjectKey` kolu `crm-visit-evidence`. Mevcut 4 kapsamın anahtarları bayt bayt aynı kalır.
2. Kapsam başına dosya kuralı:
   - uzantı ve ortam türü listesi;
   - boyut sınırı;
   - kopyalama sırasında sınır aşımı (mevcut AD-4 deseni).
   - Yalnız `CrmVisitEvidence` için tanımlanır; diğer kapsamlar mevcut genel kuralda kalır.
3. Üç kapsamlı izin anahtarı (§2.1) Platform izin kataloğunda; açıklamalar İngilizce.
4. Yetki:
   - **Yazma:** genel `.manage` veya (`visit-evidence.write` ve kapsam = `CrmVisitEvidence`).
   - **Okuma:** genel `.read`, veya (nesne kapsamı `CrmVisitEvidence` ve (`visit-evidence.read-all` veya (`visit-evidence.read` ve `CreatedBy` = çağıran))).
   - **Telafi:** genel `.manage` veya (`visit-evidence.write` ve kapsam `CrmVisitEvidence` ve `CreatedBy` = çağıran).
   - Reddedilen okuma ve telafi **404**, reddedilen yazma (yanlış kapsam) **403**.
5. Denetleyici öznitelikleri genel VEYA kapsamlı anahtarı kabul eder. Asıl karar servistedir. Denetim kaydı (MOD-0021) mevcut desenle düşer.

**Acceptance:**
- Kapsam anahtarı ve mevcut 4 kapsamın aynılığı.
- Kapsam dosya kuralı: `.pdf` → 400; 11 MB → 400 ve yarım dosya silinir.
- Yetki matrisi testleri:
  - kapsamlı yazar `Documents`'a yazamaz;
  - kapsamlı okuyucu başkasının kanıtını 404 alır;
  - `read-all` okur;
  - genel `.read` davranışı aynı;
  - telafi yalnız sahibi.
- Kiracılar arası 404 korunur.
- `DocumentRepository*` testleri yeşil; mevcut `TaskAttachments` ve `ContentMessagingArtifacts` testleri yeşil.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- `BuildObjectKey` kolu silinsin → kırmızı.
- Sahip kontrolü kaldırılsın → kırmızı.
- Kapsamlı yazarın kapsam kontrolü kaldırılsın → kırmızı.

## W3-BE-a — ziyaret yürütme kaydı (CRM)
1. **`VisitExecution` toplamı** (kiracı, plan başına tek; benzersiz dizin `(TenantId, PlannedVisitId)`).
   - Alanlar §2.2 DTO'dan.
   - `RegisterClassMaps`'e eklenir (GUID tuzağı).
   - Bilinmeyen BSON alanı yazma yok.
2. **`VisitEvidencePolicy`** kiracı belgesi + `GET` / `PUT` + doğrulama sınırları. Okumada belge yazılmaz.
3. **Başlat:**
   - sahiplik (`ICallerScope`), plan `confirmed`, `PlannedDate` = bugün (W1'deki "bugün" hesabı ile aynı kaynak), yürütme yok, rapor yok, 48 sa kilidi yok;
   - konum: Account `Latitude` / `Longitude` ile Haversine mesafe. Hedef kişi ise planın `AccountId`'si; hesap/eczane hedefinde `TargetId`;
   - ayar kuralları §0 CT 1–3 ve §2.2 hata kodları;
   - fotoğraf (`photoMode`'a göre): önce depoya yüklenir (`IVisitEvidenceStore`, `HttpContentArtifactStore` deseni, kapsam `CrmVisitEvidence`, kullanıcı token'ı iletilir, **başarısızlık kapalı**). Sonra kayıt yazılır; kayıt yazılamazsa telafi silmesi;
   - **sunucu saati** `startedAt`.
4. **Yakalama ve bitir:**
   - `capture` yalnız `started` iken ve tamamını değiştirir; `finish` son yakalamayı yazar, **sunucu saati** `finishedAt`;
   - doğrulama: ürünler planın ürünleri, adımlar planın adımları, saniye 0–86400, bayrak / ilgi sözlükleri, itiraz kategorisi BRD setinde, not uzunlukları;
   - ortak ziyaretçi: en çok 3, çağıran olamaz, kiracı kullanıcısı olmalı (Auth ad çözücü ile; çözülemezse 400);
   - `shortVisit` = `durationConsistency` ve süre < `shortVisitSeconds`.
5. **İmza:**
   - `finished` iken, rapor gönderilmemişken ve 48 sa içinde; tek sefer;
   - png, depoya `CrmVisitEvidence`; `context` = `sample` (herhangi bir üründe `sample_request`) veya `approval`.
6. **Okumalar:**
   - `by-plan` (kişisel not yalnız sahibine);
   - `brief` (son rapor: tarih, sonuç, takip notu, numuneler; yoksa boş);
   - `photo` / `signature` akış vekili: CRM sahipliği **ve** Platform kapsamlı izni; içerik kimliği yanıtta yok.
7. **Durum ve takvim:**
   - `VisitWorkStatus.Derive` yürütmeyi alır: `in_progress` `today`'den önce. Sözleşme listesine eklenir.
   - Takvim ve çalışma alanı öğesine `executionId`, `executionStatus`, `startedAt`, `finishedAt`.
   - Adım DTO'suna `minutes` ve `contentId`.
   - W1 testlerindeki sabit liste ve `in_progress → 400` testi bilinçli güncellenir (§37'de yazılır).
8. **Rapor bağı** (§2.2): W1 / W2 komutları yürütmeyi okur; `ExecutedAt` = sunucu `startedAt`.
9. **İzinler:** üç anahtar `CrmManifestProvider`'da (İngilizce açıklama). Seed / grant yok.

**Acceptance:**
- Başlat kuralları:
  - bugün değil → 409; onaysız → 409; ikinci başlatma → 409;
  - konum zorunlu + yok / uzak / doğruluk kötü → 409;
  - bilgi modunda kaydedilir + bayrak;
  - kurum konumsuz: `allow_flagged` → başlar + bayrak, `block` → 409;
  - fotoğraf zorunlu + yok → 400.
- Fotoğraf yükleme hatası → 503 ve kayıt yok; kayıt hatası → telafi çağrılır (sahte depo).
- Sunucu saati: istemci zamanı yok sayılır.
- Yakalama doğrulamaları; bitirme sonrası yakalama → 409; kısa ziyaret bayrağı.
- İmza: bitmeden → 409; ikinci → 409; `sample` bağlamı.
- Kişisel not başkasına boş.
- Yabancı plan → 404; `read-all` okur.
- `in_progress` türetme; takvim alanları; adım `minutes`.
- Rapor bağı: başlamış ziyarette `missed` → 409, bitmemişte gönderim → 409, `ExecutedAt` = `startedAt`.
- Ayar: varsayılanlar, sınırlar, okuma yazmaz.
- Mimari: yeni komutlar sayılır (27 → yeni sayı §37'de).

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- Mesafe kontrolü kaldırılsın → kırmızı.
- `startedAt` istemciden alınsın → kırmızı.
- Kişisel not herkese dönsün → kırmızı.
- Telafi çağrısı kaldırılsın → kırmızı.

## W3-BE-b — sunum notları + itiraz kütüphanesi + ziyaret sunum okuması (CRM + Platform seed)
1. `KnowledgePathStep.presentation` (§2.3).
   - Yol sürümü kopyalamada (yeni sürüm klonu) korunur; yayın dondurmasında değişmez.
   - Mevcut yol uç noktaları alanı alır / döndürür (eklemeli; eski istemci alanı göndermezse mevcut değer silinmez mi silinir mi, mevcut PUT anlamına göre; §37'de yazılır).
   - Bağlı `objectionResponseIds` yalnız aynı kiracının kayıtları.
2. **`ObjectionResponse` toplamı** + CQRS + denetleyici.
   - SoD onay; onaylı değişmez; yalnız onaylı iddialar bağlanır.
   - `RegisterClassMaps`.
3. **BRD seti `visit-objection-category`:** 9 değer, 7 dil, `crm-visit-reference.json` (veya `visit-outcome-reason`'ın bulunduğu katalog) + yükleyici kaydı. Kodda sabit etiket yok.
4. **Ziyaret sunum okuması** (§2.3):
   - slayt seçimi kuralı; dondurulmuş sürümden okuma;
   - madde işaretleri = adımın / içeriğin bağlı **onaylı** iddiaları (onaysız iddia asla);
   - itirazlar = adımın `objectionResponseIds` içindeki **onaylı** kayıtlar;
   - kategori etiketleri istek dilinde; uyarılar;
   - görsel vekili: karar 8 (okunamıyorsa `none`, raporla).

**Acceptance:**
- Sunum alanları:
  - sınırlar;
  - yeni sürüm klonu kopyalar;
  - yayınlanmış sürüm değişmez.
- İtiraz kaydı:
  - SoD (yazar onaylayamaz);
  - onaylı kayıt değişmez;
  - onaysız iddia → 400.
- Sunum okuması:
  - tanıtım tüm adımlar, hatırlatma ilk adım, eczane ilk adım;
  - onaysız iddia ve onaysız itiraz görünmez;
  - yabancı plan → 404;
  - `targetSeconds` geri düşüşü;
  - dil etiketi.
- BRD seti yükleyici testi (değer + 7 dil).
- Mimari: yeni komutlar sayılır.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- Onay durumu süzgeci kaldırılsın (iddia veya itiraz) → kırmızı.
- Hatırlatma ilk adım kuralı kaldırılsın → kırmızı.
- SoD kontrolü kaldırılsın → kırmızı.

## W3-WEB-a — çalışma alanı: başlat (E3), `in_progress`, eylemler (Web)
> Mockup ile **görsel** karşılaştırarak yap: "E3 · Ziyarete başla — kanıt" (masaüstü), "E3 · Kamera + konum" (telefon), E1 ayrıntı paneli `hazir` / `devam`.

1. **Durum:** `in_progress` `STATUSES`, `STYLE` (bilgi rengi, nabız noktası), açıklama satırı ve süzgeçte. Kartta başlangıçtan beri geçen dakika rozeti.
2. **Ayrıntı paneli eylemleri:**
   - bugün + onaylı + yürütme yok → **"Ziyarete başla"** (birincil, tam genişlik), yanında mevcut Yapılamadı / Ertele / İptal;
   - `in_progress` → **"Sunuma dön"** ve **"Ziyareti bitir"**. İkisi de sunum sayfasına gider; bitir `&phase=summary` ile ve sunum sayfası `finish` çağırır;
   - ipucu metinleri mockup'taki gibi ("Kuruma vardığınızda…", "Devam ediyor · N dk · {saat}'da başladı (sunucu saati)").
3. **E3 penceresi** (masaüstü / tablet kipli pencere, telefon tam ekran):
   - **fotoğraf:** "Dosya yükle" + "Web kamerası" (`getUserMedia`); telefonda arka kamera; JPEG'e çevir (en uzun kenar 1600 px, kalite 0,85); KVKK uyarısı;
   - **konum:** `navigator.geolocation` (alınıyor / tamam / reddedildi / kullanılamıyor / zaman aşımı); sunucu mesafeyi hesaplar, istemci yalnız gösterir;
   - **"Başlangıç: sunucu saati"** metni;
   - zorunlu / isteğe bağlı etiketleri `contract.policy`'den;
   - "Eksik: fotoğraf, konum" ipucu; "Sunumu başlat" pasif kuralı (ayar + eksikler);
   - `POST start` hata kodları kullanıcı diline çevrilir (uzak: "Kurumdan X km uzaktasınız" + açıklama);
   - başarıda sunum sayfası açılır;
   - mockup'taki tasarım notu kutusu (kiracı ayar düğmeleri) ve QR / anti-fraud kartları **çizilmez**;
   - kamera / konum izni reddi ve yükleme hatası durumları çizilir.
4. **Web vekili:** `start` (multipart akış) ve yürütme `contract`; izin sabitleri; sayfa erişim kuralı aynı (UAS-001).
5. 7 dil yankısız; Arapça RTL.

**Acceptance:**
- Saf kurallar Node'da:
  - eylem seçimi (bugün / `in_progress` / bitmiş);
  - başlat düğmesi pasif kuralı (ayar × eksik);
  - hata kodu → metin;
  - `in_progress` stili.
- Duman testi (sahte DOM):
  - "Ziyarete başla" → E3 açılır;
  - fotoğraf ve konum sahte;
  - `start` çağrısı multipart alanları doğru;
  - başarıda sunum adresi;
  - konum reddi metni;
  - `createError` null.
- Vekil: `start` izin kontrolü.
- 7 dil.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- Başlat pasif kuralı her zaman açık olsun → kırmızı.
- `in_progress` eylemleri kaldırılsın → kırmızı.

## W3-WEB-b — sunum sayfası: E4a doktor, E4b kumanda, E4c bitiş + imza (Web)
> Mockup ile **görsel** karşılaştırarak yap:
> - "E4 · Sunum — bölünmüş görünüm" (masaüstü);
> - "E4a · Doktor ekranı (sunum)" (tablet);
> - "E4b · Temsilci ekranı (kumanda)" (telefon);
> - "E4c · Bitiş özeti" / "+ imza".

1. **Sayfa ve erişim:**
   - `/CRM/VisitPresentation?plannedVisitId=` (`VisitPresentationController`, UAS-001: yetkisize iskelet yok, yönlendirme yok);
   - açılışta `visit-presentation` + `visit-execution/by-plan` + `brief` okunur;
   - yürütme `started` değilse bilgi notu ("Ziyaret başlatılmamış" + çalışma alanına bağlantı), otomatik yönlendirme yok.
2. **Yerleşim:**
   - masaüstü bölünmüş (doktor alanı + 380 px temsilci paneli);
   - tablet doktor ekranı tam ekran + kenar tutamacı;
   - telefon yalnız temsilci paneli;
   - üst çubuk: geri, ad + "{kurum} · başladı {saat}", canlı çip "Devam ediyor · mm:ss", Yüz yüze / Uzaktan.
3. **E4a doktor ekranı:**
   - ürün sekmeleri, üst satır (ürün · form · hatırlatma), başlık, alt başlık, madde işaretleri (onaylı iddialar), görsel veya yer tutucu, referans dipnotu, ilerleme noktaları, "n / N";
   - kaydırma (50 px);
   - **temsilci notları doktor alanında ASLA çizilmez.**
4. **PIN (tablet):**
   - doktor moduna geçişte temsilci 4 haneli PIN belirler (yalnız bellekte, özet);
   - kenar tutamacı veya "Çıkış: basılı tut" (650 ms) → PIN → temsilci paneli açılır;
   - PIN ile çıkış **ziyareti bitirmez**; "Doktor moduna dön" paneli kapatır.
5. **E4b kumanda:**
   - süre şeridi (ürün sayacı + toplam), mini önizleme, Geri / İleri / Ürün atla;
   - **Notlar sekmesi:**
     - anahtar mesaj;
     - "Bu slayt mm:ss · hedef ≈ X" + çubuk (hedef aşılınca uyarı rengi; Türkçe ondalık virgül);
     - "Konuşma metni · onaylı";
     - "Bu doktor için" (`brief`; ürünün ilk slaytında);
     - "Beklenen itirazlar · onaylı yanıt" akordiyonu;
     - "Kendi notum · yalnız siz görürsünüz";
     - "Geçiş".
   - **İşaretler sekmesi:** İlgili / Nötr / İlgisiz, bayrak çipleri, "İtiraz etti" → kategori seçici (`objectionCategories`).
   - **Not al sekmesi:**
     - ziyaret notu; "Sesle not" yalnız tarayıcıda Web Speech API varsa (yoksa gizli);
     - "Yöneticiyle birlikte (ortak ziyaret)" + mevcut kullanıcı seçici deseni (en çok 3; repoda mevcut kişi seçici bileşeni kullanılır, yoksa DUR + raporla);
     - süre tutarlılığı metni ayar açıksa.
   - **Sayaçlar:**
     - ürün ve slayt başına; sayfa gizliyken durur;
     - yakalama 30 sn'de bir ve slayt değişiminde (ertelemeli) `PUT capture`;
     - sayfa yeniden açılınca yürütmedeki değerlerden devam.
6. **Bitir:**
   - "Ziyareti bitir" (onay sorusu) → `POST finish` → E4c.
   - `&phase=summary` ile gelindiyse ve yürütme `started` ise önce `finish` çağrılır (mockup boşluğu 1).
7. **E4c:**
   - "Ziyaret bitti", "{ad} · {kurum} · başlangıç–bitiş";
   - SÜRE kartı (toplam + ürün satırları, çubuk, ilgi / bayrak etiketleri);
   - kısa ziyaret uyarısı (sunucu `shortVisit`);
   - NOTLAR (not + ortak ziyaretçiler);
   - **imza kartı** (telefon dışında; `doctorConfirmation` veya `sample_request` varsa): tuval + "Temizle" + "Görüşmeyi onaylıyorum" → `POST signature` (png);
   - düğmeler "Rapor gir" (mevcut rapor sayfası) ve "Sonra gireceğim · 48 sa içinde" → çalışma alanı;
   - **ön sipariş düğmesi YOK.**
8. **Web vekilleri:** sunum, yürütme okuma, `brief`, `capture`, `finish`, `signature` (multipart), kişi arama (mevcut uç). İzin sabitleri.
9. 7 dil yankısız; Arapça RTL (kaydırma yönü dahil).

**Acceptance:**
- Saf kurallar Node'da:
  - slayt gezinmesi ve "Ürün atla";
  - sayaç birikimi (görünürlük duraklatma);
  - hedef süre biçimi (tr virgül);
  - yakalama gövdesi kurucu (§2.2 şekli);
  - imza kartı görünürlük kuralı;
  - PIN durum makinesi (belirle → kilitle → aç; çıkış bitirmez).
- Duman testi (sahte DOM):
  - doktor alanında temsilci notu metni YOK;
  - E4b sekmeleri;
  - itiraz → kategori;
  - bitir → `finish` → E4c;
  - imza → `signature` çağrısı;
  - `phase=summary` başlamış yürütmede `finish` çağırır.
- Vekil izinleri; 7 dil.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- Doktor alanına konuşma metni çizilsin → kırmızı.
- PIN çıkışı `finish` çağırsın → kırmızı.
- Yakalama gövdesinde `slideTimings` düşsün → kırmızı.

---

## §36.1 Agent Prompt — W3-PLT (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3 · W3-PLT — MOD-0262-FU01 belge deposuna ziyaret kanıtı kapsamı (CrmVisitEvidence) + kapsamla sınırlı izinler (visit-evidence.write/read/read-all)
Repository: C:\tmp\vw-w3-plt (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3-plt · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-visit-execution.md — önce oku (§0 kararlar, §2.1 SABİT sözleşme, "W3-PLT" + Acceptance). Bağlam: execution/domains/platform-shared-services/module-packs/MOD-0262-FU01-document-binary-store.md (AD-4 akış, AD-5 yetki servis sınırında, AD-6 purge yok), WP-SCMM-16A (ContentMessagingArtifacts kapsamı ekleme deseni). Kod: services/Diten.Platform/src/** — Application/Contracts/DocumentRepository/{IContentStorageGateway.cs, ContentStorageOptions.cs}, Application/Features/DocumentRepository/** (DocumentRepositoryModels.cs izinler, Services/DocumentRepositoryService.cs), Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs (BuildObjectKey), API/Controllers/DocumentRepositoryController.cs; testler services/Diten.Platform/tests/** (DocumentRepository*, guard testleri).
NE: (1) ContentStorageScope.CrmVisitEvidence = 4 + BuildObjectKey kolu "crm-visit-evidence" (mevcut 4 kapsam bayt-aynı). (2) yalnız bu kapsam için dosya kuralı: .jpg/.jpeg/.png + image/jpeg|png, en çok 10 MB (ContentStorage:Scopes:CrmVisitEvidence:MaxFileSizeBytes), sınır kopyalama SIRASINDA, aşımda yarım dosya silinir. (3) izin anahtarları platform.document-repository.visit-evidence.write / .read / .read-all — Platform izin kataloğunda (mevcut anahtarların tanımlandığı yer + Platform'un izin listesi/manifesti nerede ise), açıklama İNGİLİZCE. (4) yetki servis sınırında: yazma = genel .manage VEYA (visit-evidence.write VE kapsam CrmVisitEvidence); okuma = genel .read VEYA (kapsam CrmVisitEvidence VE (read-all VEYA (read VE CreatedBy = çağıran))); telafi = genel .manage VEYA (write VE kapsam CrmVisitEvidence VE CreatedBy = çağıran). Reddedilen okuma/telafi 404, yanlış kapsama yazma 403. (5) denetleyici öznitelikleri genel VEYA kapsamlı anahtarı kabul eder (karar serviste); denetim (MOD-0021) mevcut desen.
KORU/YAPMA: ARCH GATE; TenantId (kiracılar arası 404 korunur); genel .read/.manage davranışı DEĞİŞMEZ; objectKey asla istemciye/girdi olarak yok; seed/grant/DB yazımı YOK; CRM ve Web'e DOKUNMA; ocelot değişmez (rotalar mevcut).
DOĞRULA (E2): Platform DocumentRepository* + TaskAttachment* + ContentMessagingArtifacts testleri yeşil (tam Platform paketinde bilinen ortamsal kırmızılar var: macOS mongod yolu, BSON Timestamp, Mod0029Fu29a — bunları sayıyla ayır, yeni kırmızı olmamalı) · mimari testleri değişmez. Testler belge Acceptance; sabotaj 3 (BuildObjectKey kolu sil → kırmızı; sahip kontrolü kaldır → kırmızı; kapsamlı yazarın kapsam kontrolü kaldır → kırmızı; geri al — git checkout -- YOK, .bak + touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(platform): WP-VW-W3-PLT — document repository CrmVisitEvidence scope + scope-bound visit-evidence permissions (write/read own/read-all)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt (yetki matrisi tablosu), izin anahtarlarının tanımlandığı dosya, temsilci/yönetici rolüne verilecek anahtarlar (grant betiği için), elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-BE-a (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3 · W3-BE-a — ziyaret yürütme kaydı (başlat/yakalama/bitir/imza, sunucu saati, konum mesafesi, fotoğraf+imza MOD-0262'de) + kiracı kanıt ayarı (K-W2) + in_progress durumu + rapor bağı
Repository: C:\tmp\vw-w3-be-a (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3-be-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-visit-execution.md — önce oku (§0 kararlar + CT varsayılanları, §2.1 Platform adları, §2.2 SABİT sözleşme, "W3-BE-a" 1–9 + Acceptance). Mockup (davranış için): mockups/visit-workspace/decoded/02-ziyaret-kanit-sunum.html (E3 başlat kuralı, finish yakalaması). Bağlam: WP-VW-W1 §37 (VisitWorkStatus, VisitReportDeadline, W1 sabit durum listesi testleri), WP-VW-W2 + round2/3 §37 (çalışma alanı takvimi, iki adımlı outcome→submit), WP-SCMM-16B (HttpContentArtifactStore deseni). Kod: services/Diten.CrmService/src/** — Domain/Entities/{PlannedVisit,VisitReport,Account}.cs, Application/Features/{VisitReport,VisitWorkspace,PlannedVisit}/** (VisitWorkStatus.cs, VisitReportDeadline.cs, SubmitVisitReportHandler, RecordVisitOutcomeHandler, GetVisitCalendarHandler, GetWorkspaceCalendarHandler, VisitReportModels.cs VisitCalendarPlannedStepDto), Application/Common/{ICallerScope.cs, Artifacts/IContentArtifactStore.cs}, Infrastructure/{Artifacts/HttpContentArtifactStore.cs, Auth/AuthUserDisplayNameClient.cs, DependencyInjection.cs}, Persistence (RegisterClassMaps, dizinler), Api/Controllers/CRM/**; Platform CrmManifestProvider (izin anahtarları); testler services/Diten.CrmService/tests/** (VisitWorkspaceW1Tests sabit liste + in_progress→400 testi BİLİNÇLİ güncellenir).
NE: belge W3-BE-a 1–9: VisitExecution toplamı (plan başına tek, benzersiz dizin, RegisterClassMaps) · VisitEvidencePolicy kiracı belgesi + GET/PUT (okuma yazmaz) · api/crm/visit-execution: contract, by-plan, brief, start (multipart; sahiplik, onaylı, bugün, 48 sa; Haversine mesafe Account lat/lng; konum zorunlu/bilgi; konumsuz kurum allow_flagged|block; fotoğraf önce depoya — IVisitEvidenceStore, kapsam "CrmVisitEvidence", kullanıcı token'ı ileri, başarısızlık KAPALI, kayıt yazılamazsa telafi; SUNUCU saati), capture (tam değiştirme, yalnız started), finish (SUNUCU saati, shortVisit), signature (png, bitmiş + rapor yok + 48 sa, tek sefer, context sample|approval), photo/signature akış vekili (sahiplik + read-all; contentId yanıtta YOK) · doğrulamalar (ürün/adım planınkiler, sözlükler, BRD itiraz kategorisi seti "visit-objection-category" — set W3-BE-b'de eklenir, okuyucu set yoksa kategori alanını reddeder ve bunu raporla, ortak ziyaretçi ≤3 kiracı kullanıcısı) · VisitWorkStatus in_progress (today'den önce) + sözleşme listesi · takvim/çalışma alanı öğesine executionId/executionStatus/startedAt/finishedAt, adım DTO'suna minutes + contentId · rapor bağı (başlamışta missed/rescheduled/iptal 409 visit_execution_started; bitmemişte gönderim 409 visit_execution_not_finished; ExecutedAt = sunucu startedAt) · izinler crm.visit-execution.read/record + crm.visit-evidence-policy.manage CrmManifestProvider'da (İngilizce). Alan adları, uç noktalar, hata kodları §2.2'de SABİT (W3-WEB-a/b ve mobil bunlara bağlı); değiştirmen gerekirse DUR + raporla.
KORU/YAPMA: ARCH GATE (temsilci play/kampanya görmez); TenantId; mevcut mobil sözleşmesi eklemeli kalır (outcome→submit iki çağrı, Content ilk tanıtım öğesi); seed/grant/göç/canlı DB yazımı YOK; Platform koduna DOKUNMA (yalnız CrmManifestProvider izin satırları); Web'e DOKUNMA; JourneyProgress.Advance ÇAĞRILMAZ (W4); QR/anti-fraud YOK.
DOĞRULA (E2): CRM 2558/0/5 tabanı (PiiMasking_… bilinen kararsız) · mimari: yeni yazma komutları denetimsiz sayıyı artırır (27 → yeni sayı; Faz 8 kararı, listeye ekleme YOK, sayıyı raporla). Testler belge Acceptance; sabotaj 4 (mesafe kontrolü kaldır; startedAt istemciden; kişisel not herkese; telafi çağrısı kaldır → her biri kırmızı; geri al — git checkout -- YOK, .bak + touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W3-BE-a — visit execution (start/capture/finish/signature, server time, distance, evidence in MOD-0262), tenant evidence policy, in_progress status, report binding" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, hata kodu tablosu, yeni mimari sayı, kaç TR kurumunda enlem/boylam var (salt okuma sayımı yapabiliyorsan; yoksa "ölçülmedi"), mobil için yeni alanlar/uçlar, elle denenecekler (grant gereken anahtarlar dahil). §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-BE-b (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3 · W3-BE-b — içerik modeli: yol adımı sunum notları (anahtar mesaj, konuşma metni, hedef süre, geçiş) + itiraz kütüphanesi (onaylı yanıt, SoD) + BRD seti visit-objection-category + ziyaret sunum okuması (slaytlar)
Repository: C:\tmp\vw-w3-be-b (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3-be-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-visit-execution.md — önce oku (§0 kararlar 7–11, §2.3 SABİT sözleşme, "W3-BE-b" 1–4 + Acceptance). Mockup (veri için): mockups/visit-workspace/decoded/02-ziyaret-kanit-sunum.html (E4a slayt, E4b Notlar sekmesi) + decoded/00-demo-veri.js (SLIDES, NOTES, OBJ). Bağlam: MOD-0162-FU04 KnowledgePath (gömülü adımlar, ayrı yayın + dondurma, yeni sürüm klonu StepId eşleme), Claims v2 (onay durumu), WP-VW-W2-BE-b §37 (visit-outcome-reason BRD seti + Platform katalog yükleyicisi deseni), VisitContentSequenceResolver (planın dondurduğu ContentItems/Steps/PathId/PathVersion). Kod: services/Diten.CrmService/src/** — Domain/Entities/{KnowledgePath,KnowledgeContent,Claim,PlannedVisit}.cs, Application/Features/{Knowledge/**,VisitContentSequence/**,VisitWorkspace/VisitOutcomeReasons.cs}, Application/Common/ICallerScope.cs, Persistence (RegisterClassMaps), Api/Controllers/CRM/**; services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/crm-visit-reference.json (+ yükleyici kaydı); Platform CrmManifestProvider (izinler).
NE: (1) KnowledgePathStep.presentation {keyMessage ≤500, talkTrack ≤4000, targetSeconds 10–3600|null, transition ≤500, objectionResponseIds ≤10} — yol uçlarında eklemeli, yeni sürüm klonunda kopyalanır, yayında donar. (2) ObjectionResponse toplamı (productId, categoryCode, question ≤500, answer ≤4000, claimIds yalnız ONAYLI iddialar, status draft→approved→retired, SoD: yazar onaylayamaz, onaylı değişmez) + api/crm/objection-responses + izinler crm.objection-response.read/manage/approve (CrmManifestProvider, İngilizce) + RegisterClassMaps. (3) BRD seti visit-objection-category: price, reimbursement, efficacy, side_effect, dosage, access_stock, habit_competitor, insufficient_evidence, other — 7 dil etiketi, visit-outcome-reason deseni, kodda sabit etiket YOK. (4) GET api/crm/visit-presentation/{plannedVisitId}?lang= (izin crm.visit-report.read + ICallerScope sahiplik; yabancı 404): slayt seçimi ContentItems.Order — tanıtım tüm dondurulmuş adımlar, hatırlatma ilk adım, eczane hedefi her ürünün ilk adımı; adım alanları dondurulmuş PathId+PathVersion'dan; bullets = bağlı ONAYLI iddia metinleri; objections = adımın objectionResponseIds içindeki ONAYLI kayıtlar; targetSeconds yoksa EstimatedDurationMinutes×60 yoksa null; objectionCategories istek dilinde; warnings; görsel: GET .../slides/{stepId}/asset vekili — içerik görseli temsilciye YENİ Platform izni vermeden okunabiliyorsa, değilse asset.kind="none" (karar 8; hangisi olduğunu raporla). Yanıt şekli §2.3'te SABİT (W3-WEB-b ona bağlı); değiştirmen gerekirse DUR + raporla.
KORU/YAPMA: ARCH GATE; TenantId; onaysız iddia/itiraz temsilciye ASLA dönmez; mevcut yol/iddia uç davranışı eklemeli değişir; rakip kartı / pazar payı YOK (W4/W7); "Bu doktor için" YOK (W3-BE-a brief); seed/grant/canlı DB yazımı YOK (katalog JSON dosyası değişikliği serbest); Web'e DOKUNMA; VisitExecution/VisitEvidencePolicy W3-BE-a'nın (paralel) — dokunma.
DOĞRULA (E2): CRM 2558/0/5 tabanı (PiiMasking_… bilinen kararsız) · Platform BRD katalog/yükleyici testleri yeşil · mimari: yeni yazma komutları denetimsiz sayıyı artırır (27 → yeni sayı; listeye ekleme YOK, sayıyı raporla). Testler belge Acceptance; sabotaj 3 (onay süzgeci kaldır → kırmızı; hatırlatma ilk adım kuralı kaldır → kırmızı; SoD kaldır → kırmızı; geri al — git checkout -- YOK, .bak + touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W3-BE-b — path step presentation notes, objection response library (approved, SoD), visit-objection-category set, visit presentation read" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, PUT'ta presentation alanı gönderilmezse ne olduğu, görsel vekili kararı (okunabildi mi, nasıl), demo veri notu (TUTUKON/ÜROFLEKS yollarında sunum alanı yok → yazım ekranı W3-WEB-c), elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-WEB-a (paste-ready · 2. TUR, 1. tur test dalına alındıktan SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3 · W3-WEB-a — Ziyaret Çalışma Alanı: "Ziyarete başla" + E3 kanıt penceresi (fotoğraf/web kamerası/konum) + in_progress durumu + Sunuma dön / Ziyareti bitir eylemleri
Repository: C:\tmp\vw-w3-web-a (worktree, test/crm-content-visit-e2e güncel başı — W3-PLT/BE-a/BE-b içerir) · Branch: wp/vw-w3-web-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-visit-execution.md — önce oku (§0, §2.2 sözleşme, §2.4 Web adları, "W3-WEB-a" 1–5 + Acceptance) + aynı belgedeki §37 (BE-a/BE-b/PLT kabul notları; gerçek alan adları ve sapmalar orada). MOCKUP: mockups/visit-workspace/"Ziyaret Calisma Alani (standalone).html" (Ekran seçici: "E3 · Ziyarete başla — kanıt", "E3 · Kamera + konum", E1 ayrıntı paneli hazir/devam) + decoded/02-ziyaret-kanit-sunum.html, 01-takvim.html, 00-tema.css — GÖRSEL karşılaştırarak yap (dizini py -m http.server ile AYRI sekmede aç; oturum açık kullanıcı sekmesine dokunma). Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitWorkspace/** (visit-workspace.js, workspace-core.js, visit-workspace.css), Views/CRM/VisitWorkspace/**, Resources/Views/CRM/VisitWorkspace/**, Controllers/CRM/VisitWorkspaceController.cs; testler Diten.Web.Tests/Controllers/VisitWorkspace*WebTests.cs + Js/visit-workspace*-smoke.js.
NE: belge W3-WEB-a 1–5: in_progress (STATUSES/STYLE bilgi rengi + nabız, açıklama satırı, süzgeç, kartta "N dk" rozeti) · ayrıntı paneli eylemleri (bugün+onaylı+yürütme yok → "Ziyarete başla" birincil; in_progress → "Sunuma dön" + "Ziyareti bitir" → /CRM/VisitPresentation?plannedVisitId=…[&phase=summary]; ipucu metinleri mockup'taki gibi) · E3 penceresi (masaüstü/tablet kipli, telefon tam ekran; Dosya yükle + Web kamerası getUserMedia, telefonda arka kamera; JPEG en uzun kenar 1600 px kalite 0,85; KVKK uyarısı; konum navigator.geolocation durumları; "sunucu saati" metni; zorunlu/isteğe bağlı contract.policy'den; "Eksik: …"; Sunumu başlat pasif kuralı; POST start multipart; hata kodları kullanıcı diline; başarıda sunum sayfası; izin reddi + yükleme hatası durumları; tasarım notu kutusu ve QR/anti-fraud kartları ÇİZİLMEZ) · Web vekili start (multipart akış) + yürütme contract, izin sabitleri, UAS-001 · 7 DİL yankısız, Arapça RTL.
KORU/YAPMA: ARCH GATE; W2 davranışı (takvim, Planla, E2, iki adımlı Kaydet) DEĞİŞMEZ; VisitPresentation dosyalarına DOKUNMA (W3-WEB-b paralel); CRM/Platform koduna dokunma; yeni JS kütüphanesi yok; DitenCalendar.create sırası; oturum açık kullanıcı sekmesine mock/harness enjekte etme, canlıda ziyaret başlatma (kalıcı yazma) YOK.
DOĞRULA (E2): Web 867/0 tabanı · Ziyaret Planlama testleri yeşil · mimari değişmez. Testler belge Acceptance (saf kurallar Node'da; duman testi sahte DOM: başla → E3, sahte fotoğraf/konum, start multipart alanları, başarıda sunum adresi, konum reddi metni, createError null; vekil izin; 7 dil); sabotaj 2 (başlat pasif kuralı hep açık → kırmızı; in_progress eylemleri kaldır → kırmızı; geri al — git checkout -- YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W3-WEB-a — visit workspace start visit (E3 evidence dialog: photo, webcam, location), in_progress status and actions" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde madde yapıldı / bilinçli sapma, mockup'la görsel karşılaştırma notu, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-WEB-b (paste-ready · 2. TUR, 1. tur test dalına alındıktan SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3 · W3-WEB-b — sunum sayfası /CRM/VisitPresentation: E4a doktor ekranı (tablet, PIN), E4b temsilci kumandası (notlar, işaretler, not al, sayaçlar, yakalama), E4c bitiş özeti + imza
Repository: C:\tmp\vw-w3-web-b (worktree, test/crm-content-visit-e2e güncel başı — W3-PLT/BE-a/BE-b içerir) · Branch: wp/vw-w3-web-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-visit-execution.md — önce oku (§0 kararlar 6–9, §2.2–2.4 sözleşme, "W3-WEB-b" 1–9 + Acceptance) + aynı belgedeki §37 (BE-a/BE-b kabul notları; gerçek alan adları ve sapmalar orada). MOCKUP: mockups/visit-workspace/"Ziyaret Calisma Alani (standalone).html" (Ekran seçici: "E4 · Sunum — bölünmüş görünüm", "E4a · Doktor ekranı (sunum)", "E4b · Temsilci ekranı (kumanda)", "E4c · Bitiş özeti", "E4c · Bitiş özeti + imza") + decoded/02-ziyaret-kanit-sunum.html, 00-tema.css — GÖRSEL karşılaştırarak yap (dizini py -m http.server ile AYRI sekmede aç; oturum açık kullanıcı sekmesine dokunma). Desen: frontend/Diten.Web/{Controllers/CRM/VisitWorkspaceController.cs, Views/CRM/VisitWorkspace/**, wwwroot/assets/js/CRM/VisitWorkspace/workspace-core.js} (UAS-001, vekil, L10n köprüsü, saf çekirdek + Node testi deseni); repoda mevcut kişi seçici bileşenini bul (ör. görev atanan seçici). YENİ dosyalar: Controllers/CRM/VisitPresentationController.cs, Views/CRM/VisitPresentation/**, Resources/Views/CRM/VisitPresentation/** (8 resx), wwwroot/assets/js/CRM/VisitPresentation/** (presentation-core.js saf kurallar + visit-presentation.js + .css), Diten.Web.Tests/Controllers/VisitPresentation*WebTests.cs + Js/visit-presentation-smoke.js.
NE: belge W3-WEB-b 1–9: sayfa + UAS-001 (yürütme started değilse bilgi notu, yönlendirme yok) · yerleşim masaüstü bölünmüş / tablet doktor tam ekran + kenar tutamacı / telefon yalnız kumanda; üst çubuk canlı çip + Yüz yüze/Uzaktan · E4a (ürün sekmeleri, üst satır, başlık, alt başlık, onaylı madde işaretleri, görsel/yer tutucu, referans, noktalar, n/N, kaydırma 50 px; temsilci notları doktor alanında ASLA) · PIN (geçişte belirle, bellekte özet, 650 ms basılı tut → PIN → panel; çıkış ziyareti BİTİRMEZ; "Doktor moduna dön") · E4b (süre şeridi, mini önizleme, Geri/İleri/Ürün atla; Notlar: anahtar mesaj, slayt sayacı + hedef + çubuk (tr virgül), konuşma metni, Bu doktor için (brief, ürünün ilk slaytı), itiraz akordiyonu, kendi notum, geçiş; İşaretler: ilgi + bayraklar + itiraz kategorisi seçici (objectionCategories); Not al: not, Sesle not yalnız Web Speech API varsa, ortak ziyaret + mevcut kişi seçici ≤3 (yoksa DUR + raporla), süre tutarlılığı metni) · sayaçlar ürün+slayt, gizliyken durur, PUT capture 30 sn + slayt değişiminde (ertelemeli), yeniden açılışta devam · bitir (onay) → POST finish → E4c; phase=summary + started → önce finish · E4c (süre kartı + etiketler, sunucu shortVisit uyarısı, notlar + ortak ziyaretçiler, imza kartı telefon dışında doctorConfirmation veya sample_request varsa: tuval + Temizle + Görüşmeyi onaylıyorum → POST signature png; Rapor gir + Sonra gireceğim; ön sipariş YOK) · vekiller (sunum, yürütme okuma, brief, capture, finish, signature multipart, kişi arama mevcut uç) + izin sabitleri · 7 DİL yankısız, Arapça RTL (kaydırma yönü dahil).
KORU/YAPMA: ARCH GATE (temsilci play/kampanya görmez); VisitWorkspace dosyalarına DOKUNMA (W3-WEB-a paralel; durum stili gerekiyorsa kendi CSS'inde); CRM/Platform koduna dokunma; yeni JS kütüphanesi yok (imza tuvali düz canvas); kenar menüsüne giriş YOK (sayfaya çalışma alanından gelinir); oturum açık kullanıcı sekmesine mock/harness enjekte etme, canlıda bitir/imza (kalıcı yazma) YOK.
DOĞRULA (E2): Web 867/0 tabanı · mimari değişmez. Testler belge Acceptance (saf kurallar Node'da: gezinme + ürün atla, sayaç + görünürlük duraklatma, hedef süre biçimi tr virgül, yakalama gövdesi §2.2 şekli, imza kartı kuralı, PIN durum makinesi; duman testi sahte DOM: doktor alanında temsilci notu YOK, E4b sekmeleri, itiraz → kategori, bitir → finish → E4c, imza → signature, phase=summary finish; vekil izinleri; 7 dil); sabotaj 3 (doktor alanına konuşma metni → kırmızı; PIN çıkışı finish çağırsın → kırmızı; yakalamada slideTimings düşsün → kırmızı; geri al — git checkout -- YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W3-WEB-b — visit presentation page (doctor screen with PIN, rep remote with notes/marks/timers/capture, finish summary with signature)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde madde yapıldı / bilinçli sapma, mockup'la görsel karşılaştırma notu, kişi seçici kaynağı, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — 1. tur: W3-PLT ∥ W3-BE-a ∥ W3-BE-b E2 ACCEPTED (2026-10-12)

**Test dalına alınanlar (çakışmasız):**
- `8b1fe5d92` PLT (ajan `79f492ac8`);
- `64be7741f` BE-a (ajan `298f37339`);
- `af7756cf4` BE-b (ajan `79b143932`);
- CT düzeltmesi `e9b88bf85`.

**CT ölçümü:**
- **CRM 2608/0/5** = 2558 + 46 (BE-a) + 4 (BE-b).
- **Platform** ilgili testler (DocumentRepository, CrmVisitEvidence, TaskAttachment, HasPermission, CrmManifest, CrmVisit*) **154/0**.
- **Mimari:** denetimsiz sayı **27 → 36** (+5 BE-a Start / Capture / Finish / Sign / UpdatePolicy, +4 BE-b ObjectionResponse Create / Update / Approve / Retire). Faz 8 kararı; listeye ekleme yok.
- **Gateway 86/1:** tek kırmızı 5063–5065 portları (önceden var). Yeni rotalar 5061.
- Web'e dokunulmadı (867/0 taban).

**CT kod okuması:**
- **PLT:**
  - Yetki kararı tek yerde (`DocumentRepositoryAccessPolicy`).
  - HTTP yolu yalnız `*ForCallerAsync`; Tasks süreç içi yol değişmedi.
  - `IActorPermissionContext` Program.cs'te kayıtlı ve `[HasPermission]` ile aynı eşleyiciyi kullanıyor (genel `.read/.manage` çağıranları — SCMM-16B — reddedilmez).
  - Kapsam dosya kuralı yalnız `CrmVisitEvidence`.
- **BE-a:**
  - Başlatma sırası sözleşmedeki gibi; fotoğraf önce depoya, kayıt yazılamazsa telafi; `startedAt` sunucu saati.
  - Konum kuralı (eksik / doğruluk / uzak / kurum konumsuz) ayar moduna göre ret veya bayrak.
  - Rapor bağı tek yerde (`VisitExecutionReportBinding`).
- **BE-b:**
  - Onaysız iddia, onaysız ülke sürümü ve onaysız itiraz yanıtı temsilciye dönmüyor.
  - SoD (yazar ve son düzenleyen onaylayamaz); onayda iddialar yeniden denetleniyor.

**CT düzeltmesi (`e9b88bf85`):**
- Sorun: paketin "bitince mevcut kurallar" ifadesi (CT yazım belirsizliği) bitmiş ziyareti aynı gün `today`, ertesi gün **`missed`** gösteriyordu.
- Düzeltme: `VisitWorkStatus.Derive` yürütmeyi takvim gününden önce okur.
  - **bitmiş → `report_missing`** (ziyaret yapıldı, rapor bekliyor);
  - **başlamış → `in_progress`** gün geçse de;
  - 48 sa geçince `expired` (önceki kurallar).
- Test genişletildi (ertesi gün bitmiş / bitmemiş / yürütmesiz / süresi geçmiş).

**CT sabotajları** (hepsi kırmızı, `.bak` + touch ile geri alındı):

| sabotaj | sonuç |
|---|---|
| S1 bitmiş → `report_missing` dalı kapatıldı | 1 kırmızı |
| S2 yazma sahipliği kaldırıldı | 1 kırmızı |
| S3 itiraz onay süzgeci kaldırıldı | 1 kırmızı |
| S4 PLT kapsamlı okuyucunun sahip kontrolü kaldırıldı | 2 kırmızı |

**Kabul edilen sapmalar:**
- **BE-a "onaylı plan" = `planned` | `confirmed`** (hafta onayı `planned` yazıyor; katı `confirmed` hiçbir ziyareti başlatmazdı) — DOĞRU.
- `too_far` yanıtında `distance_meters=N` satırı.
- Doğruluksuz konum = "doğruluk kötü".
- Yeniden açılan haftada başlamış ziyaret korunur.
- Konum kuralı ziyaret türünden bağımsız.
- `HttpContentArtifactStore` taşıma kodu ortak yardımcıya (SCMM testleri yeşil).
- Gateway'e iki rota (`visit-execution/{everything}`, `visit-evidence-policy`).
- **BE-b:** PUT'ta `presentation` gönderilmezse mevcut notlar korunur, `{}` temizler.
- **Görsel: `asset.kind = none`** (karar 8; temsilci içerik görselini yeni izin olmadan okuyamaz) → takip **FU-W3-SLIDE-ASSET**.

**Veri:** 97c5'te **43.374 kurumun tamamında** enlem/boylam var, Türkiye sınırları içinde (BE-a salt okuma sayımı).

**⚠ Açık engel — izin kapsamı (PLT raporu):**
- Platform'un otomatik kaydı `platform.document-repository.visit-evidence.*` anahtarlarını kapsamsız gönderiyor. Auth `platform.` önekini **PlatformAdmin** sayıyor; katalog senkronu kapsamı düşürmüyor. Kiracı rolüne `AssignPermission` 403 döner.
- **CT kararı:** üç anahtar Auth'un kapalı düzeltme listesine (`TenantRouteScopeCorrections`, BL-411) eklenir ve CRM manifestinde kiracı rotasına (`/CRM/VisitWorkspace`) bağlanır.
- Gerekçe: anahtarlar yalnız ziyaret kanıtı kapsamını açar, doğası kiracı iznidir; genel `.read/.manage` PlatformAdmin kalır.
- → **W3-AUTH** promptu (aşağıda). Canlı E4'ten önce.

**Sıradaki:** W3-AUTH → 2. tur WEB-a ∥ WEB-b (bu belgedeki promptlar; çalışma yeri test dalı `e9b88bf85`+).

## W3-AUTH — ziyaret kanıtı anahtarlarını kiracı kapsamına alma (Auth + Platform manifest)
1. **Manifest:** Platform `CrmManifestProvider`, Visit Workspace sayfasının (`/CRM/VisitWorkspace`, kiracı rotası) izinlerine üç anahtarı ekler: `platform.document-repository.visit-evidence.write`, `.read`, `.read-all` (İngilizce açıklama). Böylece manifest senkronu **Tenant** kapsamı taşır.
2. **Düzeltme listesi:** Auth `TenantRouteScopeCorrections.Allowlist`'e üç giriş (rota `/CRM/VisitWorkspace`).
   - Üç kapı aynen geçerli: listede olmak, şu an PlatformAdmin olmak, kayıtlı rota kiracı rotası.
   - Genel `platform.document-repository.read` / `.manage` listeye **GİRMEZ**.
3. **Kapsamsız senkron Tenant'ı yükseltmemeli:** Platform otomatik kaydı (`Module/Scope = null`) mevcut bir **Tenant** satırını PlatformAdmin'e **yükseltmemeli**. Bugünkü davranışı ölç:
   - yükseltmiyorsa testle sabitle;
   - yükseltiyorsa DUR + raporla (genel kural değişikliği CT kararı).
4. **Testler:**
   - Allowlist testleri (`TenantRouteScopeCorrectionTests`: "tam olarak şu anahtarlar" sabitlemesi 5 anahtara; her girişin üretim manifestinde listelenen kiracı rotasında kayıtlı olduğu çapraz kontrolü — Tasks manifesti yanına CRM manifesti).
   - Planlayıcı: üç anahtar PlatformAdmin → Tenant; ikinci koşu değişiklik yapmaz; genel `.read/.manage` dokunulmaz.
   - Platform CRM manifest testi: üç anahtar Visit Workspace sayfasında.

**Acceptance:**
- AuthService Application testleri yeşil (taban ölç), ilgili Platform manifest testleri yeşil, mimari değişmez (36).
- Sabotajlar (her biri kırmızı kanıtlanır, sonra geri alınır):
  - bir anahtar listeden çıkarılsın → kırmızı;
  - genel `.manage` listeye eklensin → kırmızı.

## §36.1 Agent Prompt — W3-AUTH (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3 · W3-AUTH — platform.document-repository.visit-evidence.{write,read,read-all} anahtarlarını kiracı kapsamına al: CRM manifestinde /CRM/VisitWorkspace sayfasına bağla + Auth TenantRouteScopeCorrections (BL-411) kapalı listesine ekle
Repository: C:\tmp\vw-w3-auth (worktree, test/crm-content-visit-e2e güncel başı — W3-PLT/BE-a/BE-b içerir) · Branch: wp/vw-w3-auth · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-visit-execution.md — önce oku (§2.1, §37 1. tur "Açık engel — izin kapsamı", "W3-AUTH" 1–4 + Acceptance). Kod: services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/TenantRouteScopeCorrections.cs (BL-411; dosyanın kendi notu: ekleme CT kararıdır — bu paket o karardır), services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs (ApplyTenantRouteScopeCorrectionsAsync), Auth catalog sync (InternalPermissionsController.Sync tie-break), services/Diten.AuthService/tests/**/Authorization/TenantRouteScopeCorrectionTests.cs; services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs (Visit Workspace sayfası), Platform PlatformPermissionAutoRegistrationWorker + HasPermissionReflector (anahtarların kapsamsız gidiş yolu), Platform DocumentRepositoryModels.cs (anahtar sabitleri).
NE: (1) CrmManifestProvider Visit Workspace sayfasının (/CRM/VisitWorkspace) izinlerine üç anahtar (İngilizce açıklama) → manifest senkronu Tenant taşır. (2) TenantRouteScopeCorrections.Allowlist'e üç giriş (rota /CRM/VisitWorkspace); üç kapı aynen; genel platform.document-repository.read/.manage listeye GİRMEZ. (3) kapsamsız otomatik kaydın mevcut Tenant satırını PlatformAdmin'e yükseltmediğini ölç: yükseltmiyorsa testle sabitle; yükseltiyorsa DUR + raporla. (4) testler: allowlist "tam olarak" sabitlemesi 5 anahtar; her giriş üretim manifestinde (Tasks + CRM) listelenen kiracı rotasında kayıtlı; planlayıcı üç anahtarı PlatformAdmin→Tenant yapar, ikinci koşu değişiklik yapmaz, genel .read/.manage dokunulmaz; Platform CRM manifest testi üç anahtarı Visit Workspace sayfasında görür.
KORU/YAPMA: kapsam yalnız AŞAĞI (PlatformAdmin→Tenant), asla yukarı; genel kural/mekanizma DEĞİŞMEZ (kapalı liste kalır); rolePermissions/grant/seed yazımı YOK (düzeltme koşusu grant eklemez/silmez); canlı DB yazımı YOK; CRM/Web koduna dokunma (yalnız Platform CrmManifestProvider); izin açıklamaları İngilizce.
DOĞRULA (E2): AuthService Application testleri (taban ölç, yeni kırmızı yok) · Platform CrmManifest* testleri yeşil · mimari 36 değişmez. Sabotaj 2 (bir anahtarı listeden çıkar → kırmızı; genel .manage'i listeye ekle → kırmızı; geri al — git checkout -- YOK, .bak + touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(auth): WP-VW-W3-AUTH — visit-evidence document-repository keys to tenant scope (CRM manifest on /CRM/VisitWorkspace + BL-411 allowlist)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, kapsamsız senkron ölçümü, canlıda düzeltmenin ne zaman çalıştığı (hangi servis hangi sırayla yeniden başlamalı), grant için adımlar. §22 TÜRKÇE. K13.
```

## §37 CT kabul — W3-AUTH E2 ACCEPTED (2026-10-12)

**Test dalına alındı:** ajan `960c7fb3f` → test dalı (cherry-pick, çakışmasız).

**CT ölçümü:**
- **AuthService Application 1108/0** (taban 1099 + 9).
- **Platform CrmManifest\* 13/0.**
- Mimari 36 (değişmedi).

**CT kod okuması:**
- CRM manifestinde Visit Workspace sayfasına üç işlem (`EVIDENCE_WRITE` / `EVIDENCE_READ` / `EVIDENCE_READ_ALL`); anahtarlar Platform sabitlerinden.
- `TenantRouteScopeCorrections.Allowlist` 2 → 5 giriş, rota `/CRM/VisitWorkspace`; üç kapı aynen; genel `.read/.manage` listede yok.

**Ölçüm (madde 3):** kapsamsız senkron (`Scope=null`) mevcut satırın kapsamına dokunmuyor.
- Manifest önce gelirse satır Tenant doğar ve öyle kalır.
- Otomatik kayıt önce gelirse PlatformAdmin doğar; manifest düşüremez → düzeltme listesi yakalar.

**CT sabotajı:** listedeki `write` girişinin rotası `/Platform/VisitWorkspace` yapıldı → **4 kırmızı** (planlayıcı, manifest çapraz kontrolü, sabit liste, ikinci koşu); `.bak` + touch ile geri alındı.

**Canlı sıra:**
1. Platform yeniden başlar (manifest Tenant gönderir).
2. Auth yeniden başlar (düzeltme açılışta çalışır; tekrar zararsız).
3. Grant.
4. Kullanıcı yeniden giriş yapar.

**⚠ Açık karar (kullanıcıya soruldu):** anahtarlar Tenant olunca yetki senkronu modülün tüm anahtarlarını kiracı **Admin**'ine verir; buna `visit-evidence.read-all` (kiracıdaki tüm kanıtları okuma) dahil. `crm.planned-visit.read-all` / `crm.visit-plan.read-all` ürün sahibi kararıyla (2026-10-06) `ExplicitGrantOnlyPermissions`'ta. Aynısı öneriliyor; karar bekleniyor.

## §37 CT kabul — W3-WEB-b E2 ACCEPTED (2026-10-12)

**Test dalına alındı:** ajan `13406e0a3` → test dalı (cherry-pick, yalnız yeni dosyalar).

**CT ölçümü:**
- **Web 883/0** (867 + 16 `VisitPresentationWebTests`).
- Mimari 36.
- VisitWorkspace, CRM ve Platform'a dokunulmadı.

**CT kod okuması:**
- Vekiller CRM uçlarının izinlerini birebir istiyor: sunum `visit-report.read`, ayrıntı `planned-visit.read`, yürütme okuma `visit-execution.read`, yazma `visit-execution.record`.
- İmza multipart olduğu gibi iletiliyor; gövdede `tenantId` reddediliyor.
- PIN durum makinesi saf çekirdekte: belirle → doğrula → kilitli → aç. Çıkış `finish` çağırmıyor.

**CT sabotajı:** PIN açma kontrolü her zaman doğru yapıldı (yanlış PIN de açar) → **2 kırmızı** (A2, A4); `.bak` + touch ile geri alındı.

**Kabul edilen sapmalar:**
- Üst satırda ürün formu yok (sunum yanıtında alan yok).
- "Rapor gir" düz `/CRM/VisitExecution` (ziyaret kimliğiyle derin bağlantı yok; W4).
- İmza ayrı "İmzayı kaydet" düğmesiyle.
- Özet etiketi "İtiraz etti: …".
- Ajan görsel karşılaştırmada İşaretler sekmesindeki ezilen satırı bulup düzeltti (`grid-auto-rows: max-content`).
- E4c ve dar ekranlar görüntüyle değil, metin ve ölçüyle karşılaştırıldı (panel gizliydi) → **canlı E4'te CT görsel karşılaştırması yapılacak.**

**Takip — FU-W3-COVISITOR-LOOKUP:**
- Ortak ziyaretçi listesi Meetings katılımcı aramasından (`/api/v1/meetings/lookups/attendees`) geliyor ve `platform.meetings.create` istiyor. Temsilcide bu izin yoksa bölüm gizleniyor.
- Ayrıca liste bir kez ve tamamı yükleniyor (arama yok).
- Doğrusu: CRM tarafında kiracı kullanıcı araması (ad arama, en çok N satır, `crm.visit-execution.record` ile). W3 kapanışında ya da W4 öncesi.

**Sıradaki:** W3-WEB-a raporu → kabul → grant betiği (kullanıcı çalıştırır) → canlı E4 + mockup görsel karşılaştırması.
