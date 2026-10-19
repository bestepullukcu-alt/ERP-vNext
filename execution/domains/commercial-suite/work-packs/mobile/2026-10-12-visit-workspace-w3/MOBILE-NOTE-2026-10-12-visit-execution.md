# Mobil notu — ziyaret yürütme: başlat, kanıt, sunum, bitir, imza (2026-10-12)

> **Kimden:** CRM (CT). **Kime:** Android ekibi.
>
> **Kapsam:** test dalında hazır ve canlı denenmiş WP-VW-W3 (PLT, BE-a, BE-b, AUTH, WEB-a, WEB-b) değişiklikleri. Ayrıntılı sözleşme: [WP-VW-W3](../../WP-VW-W3-visit-execution.md) §2.
>
> **Kural:** mevcut uçlar ve alanlar değişmedi.
> - Yeni uçlar ve yeni alanlar eklendi.
> - Rapor uçlarına yürütmeyle ilgili yeni hata kodları eklendi (§6).
> - `workStatus` listesine **`in_progress`** eklendi. W1 notunda söz verildiği gibi `today`'den önce geliyor; bilinmeyen kodda uygulama çökmemeli.

## 1. Akış
1. Takvimde bugünkü, onaylı (`planned` / `confirmed`) ziyaret.
2. **Başlat:** `POST api/crm/visit-execution/start`, multipart; konum + isteğe bağlı fotoğraf.
3. Sunum sırasında **yakalama:** `PUT {id}/capture`. 30 sn'de bir ve slayt değişiminde; tam gövde, son gelen kazanır.
4. **Bitir:** `POST {id}/finish`, son yakalama gövdesiyle.
5. **İmza** (isteğe bağlı / ayara göre): `POST {id}/signature`, multipart png.
6. Ziyaret takvimde `report_missing` olur → rapor (W1 / W2 uçları; rapor ekranı W4).

**Başlangıç ve bitiş saatini SUNUCU yazar.** İstemci saati hiçbir yerde kabul edilmez.
- Çevrimdışı başlatma YOK: başlatma ve bitirme bağlantı gerektirir.
- Çevrimdışıyken yakalama gövdesini cihazda tutup bağlantı gelince `capture` / `finish` gönderebilirsiniz; ancak saat yine sunucunun saati olur.

## 2. Uç noktalar (`api/crm/visit-execution`)
| yöntem / yol | gövde | izin |
|---|---|---|
| `GET contract` | — | `crm.visit-execution.read` |
| `GET by-plan/{plannedVisitId}` | — | `.read` + sahiplik (yoksa `data: null`, 200) |
| `GET by-plan/{plannedVisitId}/brief` | — | `.read` + sahiplik |
| `POST start` | multipart (§3) | `crm.visit-execution.record` + sahiplik |
| `PUT {id}/capture` | JSON (§4) | `.record` |
| `POST {id}/finish` | JSON (§4) | `.record` |
| `POST {id}/signature` | multipart: `file` (png), `confirmed` (`true`), `context` (`approval`\|`sample`, isteğe bağlı; sunucu türetir) | `.record` |
| `GET {id}/photo` · `GET {id}/signature` | — (görüntü akışı) | `.read` + sahiplik (veya `crm.planned-visit.read-all`) |

**Sahiplik:**
- Başkasının ziyareti → **404** (yok gibi).
- Yazma işlemi yalnız ziyaretin kendi temsilcisi tarafından yapılabilir; `read-all` sahibi başkası adına yazamaz → **403 `resource_not_caller`**.

**İçerik kimliği ve dosya anahtarı hiçbir yanıtta yoktur.** Fotoğraf ve imzayı yalnız `GET {id}/photo|signature` ile okuyun.

**Platform izinleri:** temsilci rolünde şunlar olmalı:
- `platform.document-repository.visit-evidence.write`, `.read`;
- yönetici için `.read-all`.

Bu anahtarlar kiracı izinleridir (W3-AUTH). CRM fotoğrafı kullanıcının jetonuyla Platform'a yazar.

## 3. Başlat (multipart)
| alan | zorunlu | not |
|---|---|---|
| `plannedVisitId` | ✅ | GUID |
| `kind` | — | `in_person` (varsayılan) \| `remote` |
| `latitude`, `longitude` | — | **değişmez kültür** ("41.0123"); ikisi birlikte |
| `accuracyMeters` | — | sayı; yoksa doğruluk "kötü" sayılır |
| `locationError` | — | konum yoksa nedeni: `denied` \| `unavailable` \| `timeout` |
| `photo` | ayara göre | `.jpg` / `.jpeg` / `.png`, `image/jpeg` \| `image/png`, **≤ 10 MB** (`contract.maxEvidenceBytes`). Web en uzun kenarı 1600 px JPEG (0,85) gönderiyor |
| `photoCapturedAt` | — | cihaz saati (yalnız bilgi) |

- Başarı → **201** + `VisitExecutionDto` (§5).
- Kurala takılanlar §6'da.
- **`visit_already_started`** gelirse ziyaret zaten başlamıştır: `by-plan` ile okuyup sunuma geçin (Web böyle yapıyor).

**Kiracı kanıt ayarı** (`contract.policy`; K-W2):

| alan | varsayılan | anlam |
|---|---|---|
| `locationMode` | `required` | `required`: konum yok / uzak / doğruluk kötü → ret · `info`: kaydedilir + bayrak |
| `radiusMeters` | 300 | kuruma en çok uzaklık (Account enlem / boylam; Haversine) |
| `maxAccuracyMeters` | 150 | konum doğruluğu sınırı |
| `photoMode` | `optional` | `required` ise fotoğrafsız başlatma → 400 |
| `doctorConfirmation` | true | bitişte imza kartı gösterilsin |
| `durationConsistency` | true | `shortVisitSeconds` altı `short_visit` bayrağı |
| `shortVisitSeconds` | 120 | |
| `accountWithoutLocation` | `allow_flagged` | koordinatsız kurum: başlat + bayrak \| `block` → 409 |

- **Masaüstü / cihaz muafiyeti yoktur;** sunucu cihaz türüne güvenmez.
- 97c5'te 43.374 kurumun tamamında koordinat var.

## 4. Yakalama gövdesi (`capture` / `finish`)
```json
{ "kind": "in_person",
  "productTimings": [{ "productId": "…", "seconds": 120 }],
  "slideTimings":   [{ "productId": "…", "stepId": "…", "seconds": 45 }],
  "marks": [{ "productId": "…", "interest": "interested|neutral|not_interested|null",
              "flags": ["question","objection","sample_request","literature_request","follow_up"],
              "objectionCategory": "price|reimbursement|efficacy|side_effect|dosage|access_stock|habit_competitor|insufficient_evidence|other|null" }],
  "note": "…", "personalNotes": [{ "productId": "…", "text": "…" }],
  "coVisitorUserIds": ["…"] }
```
- Ürünler ve adımlar **planın** ürünleri ve dondurulmuş adımları olmalı. Saniye 0–86400.
- Not ≤ 2000; kişisel not ≤ 1000 (**yalnız sahibine döner**); ortak ziyaretçi ≤ 3, çağıran olamaz, kiracı kullanıcısı olmalı.
- İtiraz kategorisi BRD seti `visit-objection-category` (etiketler sunum okumasında, §7).
- `capture` yalnız `started` iken (sonrası → 409 `visit_execution_finished`). `finish` bitiş saatini sunucudan yazar.

## 5. `VisitExecutionDto`
| alan | tür | not |
|---|---|---|
| `id`, `plannedVisitId`, `resourceId` | | |
| `status` | `started` \| `finished` | |
| `kind` | `in_person` \| `remote` | |
| `startedAt`, `finishedAt` | ISO (UTC) | sunucu saati |
| `durationSeconds` | int? | bitince |
| `location` | obje | `latitude`, `longitude`, `accuracyMeters`, `distanceMeters`, `radiusMeters`, `mode`, `status` (`ok`\|`far`\|`missing`\|`inaccurate`\|`account_unknown`), `error` |
| `photo` | obje | `present`, `mediaType`, `byteSize`, `capturedAt` |
| `signature` | obje | `present`, `confirmed`, `context`, `signedAt` |
| `productTimings`, `slideTimings`, `marks`, `note` | | §4 |
| `personalNotes` | | yalnız sahibine dolu |
| `coVisitors` | `[{ userId, displayName }]` | |
| `shortVisit` | bool | |
| `flags` | string[] | `location_far`, `location_missing`, `location_inaccurate`, `account_location_unknown`, `short_visit` — yönetici görür |
| `reportDeadline` | ISO | |

**`brief`** ("Bu doktor için", son rapordan):
- `lastVisitDate`, `executionOutcome`, `outcomeCode`, `doctorFeedback`, `followUpRequired`, `followUpNotes`;
- `samples[{ itemType, itemId, quantity }]`.

## 6. Hata kodları
- **404:** `visit_execution_not_found`, `visit_report_planned_visit_not_found`.
- **409:**
  - `visit_not_today`, `visit_not_confirmed`, `visit_already_started`, `visit_already_reported`;
  - `visit_location_required`, `visit_location_too_far` (ek satır `distance_meters=N` → "X km uzaktasınız"), `visit_location_inaccurate`, `visit_account_location_unknown`;
  - `visit_execution_finished`, `visit_execution_not_finished`, `visit_signature_exists`, `visit_report_deadline_passed`, `visit_execution_started`, `visit_execution_concurrency_conflict`.
- **400:** `visit_photo_required`, `visit_photo_invalid`, `visit_capture_invalid`, `visit_co_visitor_invalid`, `visit_evidence_policy_invalid`.
- **403:** `resource_not_caller`.
- **503:** `visit_evidence_store_unavailable` (belge deposuna ulaşılamadı; kayıt yazılmadı).

**Rapor uçlarına etkisi (W1 / W2):**
- Başlamış ziyarete `missed` / `rescheduled` sonucu ve plan iptali → **409 `visit_execution_started`**.
- Başlamış ama bitmemiş ziyaretin raporu → **409 `visit_execution_not_finished`**.
- Bitmiş yürütme varsa raporun `executedAt` değeri sunucudaki `startedAt` olur; gönderdiğiniz değer yok sayılır.
- Hafta yeniden açılırsa başlamış ziyaret iptal edilmez (raporlu ziyaret gibi korunur).
- **İmzada 409 `visit_signature_exists`:** imza zaten kayıtlıdır (ör. ikinci cihazdan). Yürütmeyi yeniden okuyup imzalı gösterin.

## 7. Durum ve takvim
**`workStatus` sırası:**
```
cancelled > not_done > rescheduled > reported > expired > report_missing > missed > in_progress > today > planned
```
- **Bitmiş yürütme → `report_missing`.** Ertesi gün de öyle kalır; "kaçırıldı" değildir.
- **Başlamış, bitmemiş → `in_progress`.** Gün geçse de 48 sa dolana kadar öyle kalır; dolunca `expired`.

**Takvim / çalışma alanı öğesine eklenenler:**
- `executionId`, `executionStatus`, `startedAt`, `finishedAt`;
- `plannedContent[].steps[]` → `minutes`, `contentId`.

Takvim süzgecinde `workStatus=in_progress` geçerli.

## 8. Sunum okuması
`GET api/crm/visit-presentation/{plannedVisitId}?lang=tr`
- İzin: `crm.visit-report.read` + sahiplik (yabancı → 404).
- **Slaytlar:**
  - planın ürün sırasıyla;
  - tanıtım ürününde tüm dondurulmuş adımlar;
  - hatırlatma ürününde ve eczane hedefinde yalnız ilk adım.
- **Slayt alanları:**
  - `index`, `productId`, `productCode`, `productName`, `role`, `stepId`, `contentId`, `title`, `subtitle`;
  - `bullets[]` (**yalnız onaylı iddialar**), `references[]`, `asset` (`kind: none` — görsel henüz yok);
  - `presentation`: `keyMessage`, `talkTrack`, `targetSeconds` (yoksa tahmini dakika × 60, yoksa null), `transition`;
  - `objections[]`: **yalnız onaylı** yanıtlar — `categoryCode`, `categoryLabel`, `question`, `answer`, `references`.
- `objectionCategories[{ code, label }]` istek dilinde.
- `warnings[]`: `no_content`, `no_steps`, `path_version_missing`, `content_missing`, `objection_categories_unavailable`, `asset_not_available`.
- **Doktor ekranında** yalnız `title`, `subtitle`, `bullets`, `references` gösterin. `presentation` ve `objections` temsilci notudur; doktora gösterilmez.

**Bugünkü demo verisi:** çoğu ürünün onaylı içeriği yok ("Bu ziyarette sunulacak onaylı içerik yok"). TUTUKON yollarında başlık var, madde işareti ve not yok. İçerik yazım ekranı W3-WEB-c'de gelecek.

## 9. Tablet doktor modu (Web davranışı, öneri)
- Temsilci doktor moduna geçerken 4 haneli PIN belirler. PIN cihazda kalır, sunucuya gitmez.
- Çıkış: 650 ms basılı tut → PIN → temsilci paneli. **PIN'le çıkış ziyareti bitirmez;** bitirme ayrı düğme.

## 10. Henüz yok
- QR / anti-fraud (W3E; anti-fraud mobil uygulamanın işi olacak: cihaz bütünlüğü + sahte konum tespiti sonucu sunucuya gönderilecek, ayrı not gelir).
- Katmanlı kanıt ayarı (tüzel kişilik / iş birimi) + temsilci süreli istisnası (W3-BE-c).
- Ortak ziyaretçi araması için CRM kişi arama ucu (FU-W3-COVISITOR-LOOKUP).
- Slayt görseli (FU-W3-SLIDE-ASSET).
- Rapor ekranı v2 (W4).
