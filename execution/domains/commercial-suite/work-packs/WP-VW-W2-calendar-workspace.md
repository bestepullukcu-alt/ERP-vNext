# WORK PACKAGE — WP-VW-W2 · Ziyaret Çalışma Alanı: takvim (backend + Web)

> **CT (SoR), 2026-10-09.** [Yol haritası](ROADMAP-visit-workspace.md) W2.
>
> **Mockup:** `mockups/visit-workspace/` (standalone + `decoded/01-takvim.html`, `00-demo-veri.js`, `00-tema.css`). Ürün sahibinin ekran açıklaması yol haritası §1'de. Mockup var; ayrıca mockup sorulmaz.
>
> **Kullanıcı kararları:**
> - **K-W1 = A:** erteleme yeni tarihte **yeni planlı ziyaret** oluşturur.
> - **Neden kategorileri referans verisinden gelir** (2026-10-09). Değerleri biz gireriz: Platform'un katalog yükleyicisi (repo içi, idempotent) → set Platform açılışında oluşur. Kodda sabit liste yok.
> - Faz 8 en sonda: yeni yazma komutu açılırsa mimari testin listesine eklenir ve §37'de sayısı yazılır.
>
> **Ön koşul:** W1 (`workStatus`, 48 sa son tarih, iptal kilidi, raporlayan = çağıran) test dalında.
>
> **Paketler ve dağıtım:**
>
> | Paket | Kapsam | Worktree / dal | Ne zaman |
> |---|---|---|---|
> | **W2-BE-a** | takvim okuması, neden seti, iptal / yapılamadı / ertele, plan dışı ziyaret | `C:\tmp\vw-w2-be-a` / `wp/vw-w2-be-a` | 1. tur (paralel) |
> | **W2-BE-b** | saat sabiti (sürükle → gün + saat), motor | `C:\tmp\vw-w2-be-b` / `wp/vw-w2-be-b` | 1. tur (paralel) |
> | **W2-WEB-a** | takvim ekranı (Yürüt modu), ayrıntı paneli, E2 pencereleri | `C:\tmp\vw-w2-web-a` / `wp/vw-w2-web-a` | 1. tur (paralel, sözleşme bu belgede) |
> | **W2-WEB-b** | Planla modu: Hedefler paneli, sürükle-bırak, ürün uygula | sonra açılır | 2. tur (WEB-a + BE-b kabulünden sonra) |
>
> Commit dala yapılır, push YOK.

---

## W2-BE-a — CRM + Platform katalog

### A1. Neden seti (referans verisi)
- **Set:** `visit-outcome-reason`.
  - Kapsam `tenant`: kiracı kendi listesini yönetebilsin. Yükleyici tenant seti desteklemiyorsa `global` kullanılır ve raporda gerekçesi yazılır.
  - Katalog dosyası yeni: `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/crm-visit-reference.json`. `catalog_version`, `note` ve diğer katalogların biçimi aynen izlenir.
  - `appsettings.Development.json` → `BusinessReferenceData:CatalogLoad:RequiredSetCodes` listesine eklenir.
  - `appsettings.json` → `BusinessReferenceData:ConsumableSets` listesine eklenir; temsilci rolü okuyabilmeli.
  - CRM drift guard (`CrmReferenceSetDriftGuardTests`) yeşil kalır.
- **Nitelikler:**
  - `applies_to`: virgüllü, `cancel` / `missed` / `reschedule`;
  - `requires_note`: `true` / `false`;
  - dil etiketleri: 4I'de kullanılan kural (değer niteliği `label_<dil>`). Diller: tr, fr, es, zh, ar, ru (en = `display_name`). **7 dilin hepsi dolu olmalı.**
- **Değerler (mockup `REASONS` sırasıyla):**

| value_code | tr | applies_to | requires_note |
|---|---|---|---|
| `doctor_unavailable` | Doktor yok / izinde | cancel,missed,reschedule | false |
| `doctor_declined` | Doktor zamanı yok / görüşmeyi reddetti | cancel,missed,reschedule | false |
| `clinic_closed` | Kurum kapalı / girişe izin yok | cancel,missed,reschedule | false |
| `rep_unavailable` | Temsilci izinli / hasta | cancel,missed,reschedule | false |
| `meeting_conflict` | Toplantı / eğitim çakışması | cancel,missed,reschedule | false |
| `travel_weather` | Ulaşım / hava koşulu | cancel,missed,reschedule | false |
| `target_inactive` | Doktor kurumdan ayrıldı / hedef pasif | cancel,missed | false |
| `other` | Diğer | cancel,missed,reschedule | **true** |
| `rescheduled_by_doctor` | Doktor erteledi (eski) | — | false (**is_active: false**) |
| `rescheduled_by_rep` | Temsilci erteledi (eski) | — | false (**is_active: false**) |

  Son iki değer: bugünkü sabit `VisitReportReasonCodes` listesinde var. Eski raporlar okunurken adları görünsün diye pasif değer olarak tutulur; yeni yazmada kabul edilmez.
- **CRM tarafı:**
  - neden doğrulaması `IReferenceDataValidator` ile yapılır (kapalı-güvenli: set okunamazsa `503 reference_data_unavailable`, mevcut desen);
  - sabit `VisitReportReasonCodes.All` doğrulamada **kullanılmaz** (sınıf eski kodları belgelemek için kalabilir);
  - `applies_to` ve `requires_note` değer niteliklerinden okunur (`GetValueAttributesAsync`).
- **Okuma:** `GET api/crm/visit-workspace/reasons?appliesTo=cancel|missed|reschedule` → `[{ code, label, requiresNote }]`. Etiket isteğin diline göre gelir (`Accept-Language` ya da `?lang=`, 4I kuralı). Mobil de bunu kullanır.

### A2. İptal / yapılamadı / ertele (mevcut komutlara kural)
- **İptal** (`POST planned-visits/{id}/cancel`, mevcut):
  - gövdeye `reasonCode` (applies_to ∋ cancel) + `note` (≤ 500; `requires_note` ise zorunlu);
  - `PlannedVisit` ek alanları `CancellationReasonCode`, `CancellationNote`;
  - sistem iptalleri (`week_reopened`) eski `CancellationReason` alanında kalır;
  - yalnız ziyaret günü **bugün ya da gelecekse** iptal; geçmiş gün → `409 visit_cancel_past_day` ("kaçırıldı" ziyarette yapılamadı / ertele kullanılır).
  - **Geriye uyum:** `reasonCode` gelmezse eski serbest metinli istek bir sürüm daha kabul edilir. Rapora ve mobil notuna "zorunlu olacak" diye yazılır.
- **Yapılamadı / ertele** (`RecordVisitOutcome`, mevcut):
  - `missed` ve `rescheduled` sonucunda `reasonCode` referans setten doğrulanır (applies_to);
  - `other` → not zorunlu (≤ 500, mevcut `MaxReasonLength`);
  - bilinmeyen ya da pasif kod → `400 visit_reason_invalid`;
  - not eksik → `400 visit_reason_note_required`.
- **Erteleme = yeni ziyaret (K-W1 = A):**
  - `rescheduled` sonucu **kesinleştiğinde** (gönderimde; W1 kuralı: taslak sonuç sayılmaz) CRM yeni bir planlı ziyaret oluşturur:
    - aynı hedef, kurum, temsilci, içerik öğeleri (ürün adları dahil);
    - tarih = `RescheduleToDate`, saat boş;
    - `PlannedVisitSource` yeni değer **`reschedule`**;
    - bağlar: yeni ziyarette `RescheduledFromPlannedVisitId`, raporda `RescheduledToPlannedVisitId`.
  - **Tarih kuralı:**
    - bugünden sonra, çalışma günü (Working Calendar; 4E'deki çözümleyici), etkin dönemin içinde;
    - aksi halde `400 visit_reschedule_date_invalid`. Mevcut `RescheduleDateInvalid` kodu bu anlamda kullanılabilir; yeniden adlandırmaya gerek yok.
  - **İdempotent:** aynı rapor için ikinci yeni ziyaret oluşmaz.
  - **Düzeltme:** yeni ziyaret oluştuktan sonra düzeltmede `RescheduleToDate` değiştirilemez → `409 visit_reschedule_already_applied`.
  - **Atomiklik:** rapor kesinleşmesi ile yeni ziyaret yazımı ya birlikte olur ya hiç (CRM standalone Mongo işlem deseni: `SupportsTransactionsAsync` + telafi).
- **Erteleme seçenekleri (okuma):** `GET api/crm/visit-workspace/reschedule-options?plannedVisitId=` → sonraki **8 çalışma günü** `[{ date, plannedCount, capacityMinutes, plannedMinutes, isHoliday:false }]` (mockup: doluluk listesi). Kapasite kaynağı 4G gün bütçesidir.

### A3. Plan dışı ziyaret (yalnız bugün)
- `POST api/crm/planned-visits` (mevcut) gövdesine `unplanned: true` eklenir:
  - `PlannedDate` = bugün (UTC) olmalı, aksi halde `400 unplanned_visit_today_only`;
  - `Source` = yeni değer **`unplanned`**;
  - planlama oturumuna bağlı değil;
  - hedef temsilcinin kapsamında olmalı (mevcut sahiplik kuralı).
- `unplanned` verilmeyen eski manuel oluşturma **değişmez** (W9 karar verir).

### A4. Birleşik takvim okuması
- **Uç:** `GET api/crm/visit-workspace/calendar?from=yyyy-MM-dd&to=yyyy-MM-dd[&resourceId=]`.
  - Kaynak = çağıran; `resourceId` yalnız read-all sahibine.
  - Pencere ≤ 42 gün, aksi halde `400 visit_report_calendar_range_invalid`.
- **Yanıt:**
  - `visits[]`:
    - yazılmış planlı ziyaretler: W1 takvim öğesinin alanları + `source` + `rescheduledFrom` / `rescheduledTo` + `cancellationReasonCode` / `cancellationNote` + `isPinned` (gün / saat sabiti var mı);
    - temsilcinin planlama oturumunun **taslak haftalarındaki önizleme ziyaretleri**: aynı biçim, `workStatus = "draft"`, `plannedVisitId = null`, `previewKey` (hedef + hafta); `isExtra` (4L).
    - Taslak önizlemesi mevcut önizleme okumasından gelir; yeni hesap yazılmaz.
  - `weeks[]`:
    - `{ weekStart, weekNumber, state: draft|approved|past|none, planningSessionId, canApprove, canReopen }`;
    - `{ capacityMinutes, plannedMinutes, visitCount, unplacedCount }`. "N ziyaret sığmadı" = `unplacedCount`, 4E kaydırma / sığmama nedenlerinden.
    - Onay ve yeniden açma için mevcut uçlar kullanılır; bu okuma yalnız durumu verir.
  - `days[]`: `{ date, isHoliday, holidayName?, capacityMinutes, plannedMinutes, freeMinutes }`.
- **`workStatus` sözlüğüne yalnız bu uçta `draft` eklenir** (taslak hafta önizlemesi). Ziyaret raporu takvimi ve mobil W1 sözleşmesi değişmez.
- **Okuma izni:** `crm.visit-report.read` + `crm.visit-plan.read`.
- **Contract:** `GET api/crm/visit-workspace/contract` → `workStatuses` (draft dahil), `reasonSet = visit-outcome-reason`, `rescheduleOptionDays = 8`, `maxWindowDays = 42`.

### Acceptance (W2-BE-a)
1. **Katalog:** set yükleyiciyle oluşuyor (Platform katalog testi, `CrmClaimsReferenceCatalogTests` deseni); 7 dil etiketi dolu; consumable listesinde; CRM drift guard yeşil.
2. **Neden doğrulaması:** referans setten okunuyor (sahte okuyucu ile), sabit liste kullanılmıyor (sabit listede olmayan, setteki yeni kod kabul ediliyor). Pasif ya da bilinmeyen kod → 400; `other` + not yok → 400; set okunamıyor → 503.
3. **İptal:** reasonCode + not kaydı; geçmiş gün → 409; geriye uyum (kodsuz istek) çalışıyor.
4. **Erteleme:**
   - gönderimde yeni ziyaret (kaynak `reschedule`, bağlar, içerik kopyası);
   - taslakta oluşmuyor;
   - ikinci gönderimde ikinci ziyaret yok;
   - tatil / hafta sonu / dönem dışı tarih → 400;
   - düzeltmede tarih değişimi → 409;
   - atomiklik (yeni ziyaret yazımı başarısızsa rapor kesinleşmiyor ya da telafi ediliyor).
5. **Plan dışı:** bugün → 201 ve `source = unplanned`; yarın → 400; eski manuel oluşturma değişmedi.
6. **Takvim okuması:**
   - onaylı hafta ziyaretleri + taslak hafta önizlemesi (`draft`) birlikte;
   - hafta durumları;
   - `unplacedCount`;
   - tatil günü;
   - 43 gün → 400;
   - başka temsilci → 403 (read-all serbest).
7. **Erteleme seçenekleri:** 8 çalışma günü (tatil atlanıyor), sayılar doğru.
- **Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
  - neden doğrulamasını sabit listeye geri bağla → 2 kırmızı;
  - erteleme yeni ziyareti taslakta da oluştursun → 4 kırmızı;
  - plan dışı tarih kontrolünü kaldır → 5 kırmızı.
- **Mimari:** yeni yazma komutu açılmazsa 27. Açılırsa sayıyı yaz.

---

## W2-BE-b — saat sabiti (motor)
- **`PlanningDayPin`'e isteğe bağlı `StartTime`** (`HH:mm`, yalnız `Scope = visit`):
  - mevcut güncelleme isteğindeki `dayPins[]` öğesinde `startTime`;
  - doğrulama:
    - çalışma saatleri içinde, aksi halde `400 pin_time_outside_hours`;
    - 15 dakikalık adım (mockup ızgarası), aksi halde `400 pin_time_invalid`;
  - eski sabitler (saatsiz) aynen gün sabiti.
- **Motor:**
  - saatli sabit, o günün o saatine yerleşir;
  - günün diğer ziyaretleri sabitin **önüne ve arkasına** geo sırayı bozmadan dizilir (4G `AssignAroundPins` mantığının gün içi karşılığı);
  - iki saatli sabit çakışırsa ikincisi en yakın boş dilime kayar, kaydırma nedeni `pin_time_conflict`;
  - gün kapasitesi aşılırsa mevcut `pin_overflow` kuralı;
  - önizlemede ziyaret `isPinned: true`, `pinnedTime`.
- **Onaylı hafta:** sabit yalnız taslak haftaya yazılır (4E kuralı aynen; onaylıda `409`).
- **Okuma:** önizleme öğelerinde `pinnedTime`. W2-BE-a'nın takvim okuması `isPinned` / `pinnedTime`'ı buradan taşır. İki paket paralel: alan adı **`pinnedTime`** olarak sabittir.

### Acceptance (W2-BE-b)
1. 10:30 sabiti → ziyaret 10:30'da; günün diğer ziyaretleri öncesinde ve sonrasında, sıra kararlı.
2. Çalışma saati dışı / 15 dk adımı dışı → 400.
3. İki çakışan saatli sabit → ikincisi kayar + `pin_time_conflict`.
4. Saatsiz eski sabit davranışı değişmedi (4E / 4G testleri yeşil).
5. Onaylı haftaya sabit → 409.
- **Sabotaj** (kırmızı kanıtlanır, sonra geri alınır): saat yok sayılsın (yalnız gün) → 1 kırmızı.
- **Mimari:** 27.

---

## W2-WEB-a — Takvim ekranı (Yürüt modu)
- **Yeni sayfa `/CRM/VisitWorkspace`** ("Ziyaret Çalışma Alanı"):
  - menüye CRM altında, Ziyaret Planlama'nın üstüne;
  - eski sayfalar yerinde kalır (W9);
  - yetkisiz kullanıcıya iskelet çizilmez (UAS-001).
- **Takvim:** FullCalendar `frontend/Diten.Web/wwwroot/assets/vendor/libs/fullcalendar` (projede var; yeni kütüphane ekleme).
  - Masaüstü: hafta zaman ızgarası (Pzt–Cum, çalışma saatleri), gün görünümü.
  - Telefon genişliği (< 768 px): **gün listesi** (mockup).
  - Hafta değişimi üstteki hafta şeridinden.
- **Hafta başlığı (mockup):**
  - hafta adı + tarih aralığı (4I biçimleri: `VisitPlanningFormat`, `<html lang>`);
  - durum rozeti (taslak / onaylı / geçmiş);
  - kapasite çubuğu (planlanan / kapasite);
  - "⚠ N ziyaret sığmadı" (tıklayınca liste);
  - **Haftayı onayla / Haftayı yeniden aç**: mevcut uçlar; yeniden açmada gerekçe penceresi 4D'deki gibi.
- **Gün sütunu başlığı:** doluluk + boş süre, tatil.
- **Ziyaret kartı:**
  - doktor / kurum adı, saat, ürün adları (kod değil);
  - durum rengi (`workStatus`; `draft` soluk / kesikli kenar);
  - geri sayım (`reportDeadline`, `missed` / `report_missing` için);
  - sabit simgesi (`isPinned`), plan dışı simgesi (`source = unplanned`), ertelemeden gelen simgesi.
- **Süzgeçler:** durum (çoklu), kurum, ürün. Seçim tarayıcıda hatırlanır.
- **Ayrıntı paneli** (offcanvas; telefonda tam ekran):
  - durum bandı (geri sayım, "kaçırıldı: 48 saat içinde yapılamadı ya da ertele");
  - **Ne sunacağım:** ürünler; ilk ürün tanıtım, diğerleri hatırlatma;
  - **Önceki ziyaretten:** son raporun sonucu, tarihi, geri bildirimi (mevcut rapor okuması; ilgi / bağlılık W4'te gelir; alan yoksa bölüm gizli);
  - sıklık bilgisi (3D: yapılan / gereken, planlı);
  - **duruma göre eylemler:**
    - `today` / `planned`: İptal; bugünse **Sonuç gir** (W3'e kadar mevcut Ziyaret Yürütme rapor formuna gider);
    - `missed`: Yapılamadı, Ertele;
    - `report_missing`: Raporu gönder (mevcut form);
    - `expired`: kilitli, yalnız bilgi;
    - `draft`: Planla moduna geç (W2-WEB-b; o gelene kadar mevcut Ziyaret Planlama sayfasına bağlantı).
- **E2 pencereleri (iptal / yapılamadı / ertele):**
  - neden listesi `GET .../reasons?appliesTo=`;
  - `requiresNote` ise not zorunlu (sayaç 0/500);
  - **Ertele:** sonraki 8 çalışma günü doluluk listesi (`reschedule-options`), gün seçimi;
  - gönderimde W1 / W2-BE-a hata kodları kullanıcı diliyle gösterilir: `visit_report_deadline_passed`, `visit_report_plan_cancelled`, `visit_reason_note_required`, `visit_reschedule_date_invalid`, `visit_cancel_past_day`.
- **Plan dışı ziyaret düğmesi:** yalnız bugün; doktor arama (mevcut kişi okuması); `unplanned: true`.
- **Ön sipariş düğmesi:** görünmez (W6).
- **Diller:** **7 dil** resx (tenant modülü): en, tr, fr, es, zh, ar, ru. Arapçada RTL (4I `bidi` / `isolate` yardımcıları).
- **Vekil:** Web vekili `VisitWorkspaceController` (`/CRM/VisitWorkspace/api/*`), yetki anahtarları BE-a ile aynı.
- **Paralel çalışma notu:** BE-a uçları henüz yoksa ekran sözleşmeye göre yazılır. Testler vekil ve JS üzerinde yapılır (Node'da çalışan saf fonksiyonlar, önceki paketlerin deseni).

### Acceptance (W2-WEB-a)
1. Sayfa yetkisiz kullanıcıya iskelet çizmiyor; yetkili kullanıcıya takvim + hafta başlığı.
2. Kart durum eşlemesi `workStatus → renk / simge`, 10 durumun hepsi (draft dahil). Saf fonksiyon Node'da test edilir.
3. Geri sayım hesabı `reportDeadline`'dan (saat sabit test).
4. E2: neden listesi `appliesTo` ile isteniyor; `requiresNote` notu zorunlu kılıyor; erteleme seçenekleri gösteriliyor; hata kodu → kullanıcı metni eşlemesi.
5. Vekil uçları doğru CRM yollarına ve yetkilere gidiyor (vekil testi).
6. 7 dil: yeni anahtarların hepsi her dilde dolu; değer anahtar yankısı değil (NavL10n kuralı); menü anahtarı da 7 dilde.
7. Telefon genişliğinde gün listesi (saf düzen seçici fonksiyonu test edilir).
- **Sabotaj** (kırmızı kanıtlanır, sonra geri alınır): `requiresNote` yok sayılsın → 4 kırmızı.

---

## W2-WEB-b — Planla modu (2. tur; ayrı prompt, sonra verilecek)
- Planla / Yürüt geçişi.
- Hedefler paneli gömülü: 4J / 4L / 4M bileşenleri yeniden kullanılır, kopyalanmaz.
- İşaretle → en boş güne (mevcut motor).
- Doktoru güne / saate sürükle → `dayPins[].startTime` (BE-b).
- Önizleme ziyaretini başka gün / saate sürükle.
- "Ürün uygula" (ilk ürün tanıtım, diğerleri hatırlatma).
- Kapsam W2-WEB-a ve BE-b kabulünden sonra kesinleşir.

## KORU / YAPMA (tüm W2)
- ARCH GATE: temsilci play / kampanya görmez, seçmez.
- Kiracı sınırı (TenantId) her sorguda.
- "Bugün" UTC.
- W1 kuralları aynen: 48 sa, iptal kilidi, raporlayan = çağıran.
- Referans verisi değerleri **kodda sabit değil**. Etiketler referans verisinden; Web'de yedek metin yalnız "yüklenemedi" durumu için.
- Göç yok. Yeni alanlar boş okunur (class-map: yeni Guid alanları string GUID olarak kaydedilir — CRM class-map tuzağı).
- Seed / grant yok; tek istisna Platform katalog dosyası (kullanıcı kararı). Canlı veriye elle yazma yok.
- **Oturum açık kullanıcı sekmesine mock / harness enjekte edilmez.** Canlı deneme ayrı sekmede yapılır, kalıcı yazma onaysız yapılmaz.
- `dotnet test -o` çıktısı repo içinde kalır. Sabotaj geri alınırken `git checkout --` kullanılmaz; geri alınan dosyaya touch yapılır.

---

## §36.1 Agent Prompt — W2-BE-a (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W2 · W2-BE-a — takvim okuması, neden seti (referans verisi), iptal / yapılamadı / ertele (yeni ziyaret), plan dışı ziyaret
Repository: C:\tmp\vw-w2-be-a (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vw-w2-be-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-calendar-workspace.md — önce oku (W2-BE-a: A1–A4 + Acceptance + KORU/YAPMA). Bağlam: ROADMAP-visit-workspace.md (K-W1=A), WP-VW-W1 §37 (VisitWorkStatus, VisitReportDeadline, raporlayan=çağıran), WP-VP-4E/4G/4L §37 (önizleme, gün bütçesi, kaydırma nedenleri, weekExtras), WP-BRD-TENANT-CRM-SETS (consumable-sets + CrmReferenceSetDriftGuardTests), Platform katalog yükleyicisi (Seed/business-reference-data/*.json, CrmClaimsReferenceCatalogTests deseni), mockup decoded/00-demo-veri.js (REASONS). Kod: services/Diten.CrmService/src/**/Features/{VisitReport,PlannedVisit,VisitPlanning}/** · Infrastructure/ReferenceValidation/** · services/Diten.Platform/src/Diten.Platform.API/{Seed/business-reference-data,appsettings*.json}.
NE: (A1) referans seti visit-outcome-reason (tenant; yükleyici desteklemezse global + gerekçe) yeni katalog crm-visit-reference.json + RequiredSetCodes + ConsumableSets; nitelikler applies_to / requires_note / label_<dil> (7 dil dolu); 8 aktif + 2 pasif eski değer (belgedeki tablo); CRM neden doğrulaması IReferenceDataValidator ile (kapalı-güvenli 503), sabit VisitReportReasonCodes doğrulamada kullanılmaz; GET api/crm/visit-workspace/reasons?appliesTo=. (A2) iptal: reasonCode+note (requires_note), PlannedVisit.CancellationReasonCode/CancellationNote, geçmiş gün 409 visit_cancel_past_day, kodsuz eski istek bir sürüm daha kabul; missed/rescheduled reasonCode setten (400 visit_reason_invalid / visit_reason_note_required); ERTELEME = rescheduled sonucu GÖNDERİMDE kesinleşince yeni planlı ziyaret (aynı hedef/kurum/temsilci/içerik, Source=reschedule yeni değer, RescheduledFromPlannedVisitId ↔ rapor RescheduledToPlannedVisitId), tarih: bugünden sonra + çalışma günü + etkin dönem (400 visit_reschedule_date_invalid), idempotent, düzeltmede tarih değişimi 409 visit_reschedule_already_applied, atomik (SupportsTransactionsAsync + telafi); GET api/crm/visit-workspace/reschedule-options?plannedVisitId= (8 çalışma günü + doluluk). (A3) POST planned-visits unplanned:true → yalnız bugün (400 unplanned_visit_today_only), Source=unplanned; eski manuel oluşturma değişmez. (A4) GET api/crm/visit-workspace/calendar?from&to[&resourceId] (≤42 gün): visits[] (yazılmış ziyaretler W1 alanları + source/rescheduledFrom/To/cancellationReasonCode/note/isPinned/pinnedTime + taslak hafta önizlemesi workStatus=draft, previewKey, isExtra), weeks[] (state, canApprove/canReopen, capacity/planned minutes, visitCount, unplacedCount), days[] (tatil, kapasite/boş dakika); draft yalnız bu uçta; GET api/crm/visit-workspace/contract. İzin: okuma crm.visit-report.read + crm.visit-plan.read; yazmalar mevcut uçların anahtarları. pinnedTime alanı W2-BE-b'den gelir (paralel paket; alan adı sabit, yoksa null).
KORU/YAPMA: ARCH GATE; TenantId; "bugün" UTC; W1 kuralları aynen; referans değerleri kodda sabit değil; göç yok, yeni Guid alanları class-map'e string GUID; seed/grant yok (tek istisna Platform katalog dosyası); canlı veriye yazma yok; yeni yazma komutu açarsan mimari listesine ekle ve sayısını raporla (Faz 8 en sonda).
DOĞRULA (E2): CRM 2506/0/5 tabanı (PiiMasking_… bilinen kararsız) · Platform BRD katalog testleri · Web 826/0 (Web dokunulmuyorsa koşmak yeter) · mimari 27 (artarsa yaz). Testler belge Acceptance 1–7; sabotaj 3 (kırmızı kanıtla, geri al — `git checkout --` YOK, geri alınca touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W2-BE-a — workspace calendar read, visit reason set (reference data), reschedule creates a new visit, unplanned visit today only" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, set kapsamı (tenant/global) ve yükleyici bulgusu, yeni hata kodları / alanlar (mobil için), yeni yazma komutu var mı, elle denenecekler (fleet yeniden başlatınca set oluşmalı). §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W2-BE-b (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W2 · W2-BE-b — saat sabiti (sürükle → gün + saat), planlama motoru
Repository: C:\tmp\vw-w2-be-b (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vw-w2-be-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-calendar-workspace.md — önce oku (W2-BE-b + KORU/YAPMA). Bağlam: WP-VP-4E §37 (dayPins, onaylı hafta 409), WP-VP-4G §37 (DayBalancer, AssignAroundPins, kaydırma nedenleri), 4F (rota gün taşıma), 4L (weekExtras). Kod: services/Diten.CrmService/src/**/Features/VisitPlanning/** (VisitPlanningEngine, DayBalancer, PlanningSession, UpdatePlanningSessionSelection) · Domain/Entities/PlanningSession.cs (PlanningDayPin) · Api/Models/CRM/VisitPlanningRequests.cs.
NE: PlanningDayPin'e isteğe bağlı StartTime ("HH:mm", yalnız Scope=visit), güncelleme isteğinde dayPins[].startTime; doğrulama: çalışma saatleri içinde (400 pin_time_outside_hours), 15 dk adım (400 pin_time_invalid); saatsiz eski sabit = gün sabiti (değişmez). Motor: saatli sabit o saate yerleşir, günün diğer ziyaretleri öncesine/sonrasına geo sıra korunarak dizilir (AssignAroundPins'in gün içi karşılığı), iki saatli sabit çakışırsa ikincisi en yakın boş dilime + kaydırma nedeni pin_time_conflict, kapasite aşımı mevcut pin_overflow; önizleme öğesinde isPinned + pinnedTime (alan adı SABİT: pinnedTime — W2-BE-a paralel olarak bunu okuyacak). Onaylı haftaya sabit 409 (4E aynen).
KORU/YAPMA: ARCH GATE; TenantId; 4E/4G/4L davranışı ve testleri aynen yeşil; göç yok (yeni alan boş okunur, class-map); yeni yazma komutu YOK (mevcut güncelleme); canlı veriye yazma yok.
DOĞRULA (E2): CRM 2506/0/5 tabanı (PiiMasking_… bilinen kararsız) · mimari 27. Testler belge Acceptance 1–5; sabotaj 1 (saat yok sayılsın → kırmızı; geri al — `git checkout --` YOK, touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W2-BE-b — time pins (day + start time) in the planning engine" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, motor yerleşim kuralı, yeni kodlar / alanlar (mobil için), elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W2-WEB-a (paste-ready)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W2 · W2-WEB-a — Ziyaret Çalışma Alanı takvim ekranı (Yürüt modu), ayrıntı paneli, iptal / yapılamadı / ertele pencereleri
Repository: C:\tmp\vw-w2-web-a (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vw-w2-web-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-calendar-workspace.md — önce oku (W2-WEB-a + sözleşme: W2-BE-a A1–A4, W2-BE-b pinnedTime + KORU/YAPMA). MOCKUP: execution/domains/commercial-suite/work-packs/mockups/visit-workspace/ (standalone html + decoded/01-takvim.html, 00-demo-veri.js, 00-tema.css) — takvim, hafta başlığı, kart, ayrıntı paneli ve E2 pencerelerini mockup'a göre yap. Bağlam: WP-VW-W1 §37 (workStatus 9 kod, reportDeadline, managerAttention), WP-VP-4D/4I/4J §37 (yeniden açma gerekçesi, VisitPlanningFormat/bidi/isolate, RTL, liste düzeni). Kod: frontend/Diten.Web/** (yeni Controllers/CRM/VisitWorkspaceController.cs, Views/CRM/VisitWorkspace/**, wwwroot/assets/js/CRM/VisitWorkspace/**, Resources/Views/CRM/VisitWorkspace/*.resx, menü); FullCalendar: wwwroot/assets/vendor/libs/fullcalendar (projede var, yeni kütüphane ekleme).
NE: yeni sayfa /CRM/VisitWorkspace (menü CRM altında Ziyaret Planlama'nın üstü; yetkisizde iskelet yok UAS-001); FullCalendar hafta zaman ızgarası + gün, telefonda gün listesi; hafta başlığı (ad+aralık 4I biçimi, durum rozeti, kapasite çubuğu, "N ziyaret sığmadı" listesi, Haftayı onayla / yeniden aç mevcut uçlar + 4D gerekçe penceresi); gün başlığı doluluk/boş süre/tatil; kart (ad, saat, ürün ADLARI, workStatus rengi — draft soluk, geri sayım reportDeadline, sabit/plan dışı/ertelemeden simgeleri); süzgeçler (durum, kurum, ürün; tarayıcıda hatırlanır); ayrıntı paneli (durum bandı, ne sunacağım: ilk ürün tanıtım diğerleri hatırlatma, önceki ziyaretten: son rapor — alan yoksa gizli, sıklık, duruma göre eylemler: İptal / Sonuç gir → mevcut Ziyaret Yürütme formu / Yapılamadı / Ertele / Raporu gönder / expired kilitli / draft → Planla (WEB-b gelene kadar Ziyaret Planlama bağlantısı)); E2 pencereleri: neden listesi GET reasons?appliesTo, requiresNote → not zorunlu 0/500, Ertele → reschedule-options 8 çalışma günü doluluk, hata kodları kullanıcı diliyle (visit_report_deadline_passed, visit_report_plan_cancelled, visit_reason_note_required, visit_reschedule_date_invalid, visit_cancel_past_day); Plan dışı ziyaret (yalnız bugün, unplanned:true); Ön sipariş düğmesi YOK (W6). Vekil VisitWorkspaceController /CRM/VisitWorkspace/api/* → api/crm/visit-workspace/* + mevcut planned-visits/visit-report/visit-plan uçları, BE-a ile aynı yetki anahtarları. 7 DİL resx (en,tr,fr,es,zh,ar,ru) + menü anahtarı 7 dil; Arapça RTL (bidi/isolate). BE-a/BE-b paralel yazılıyor: uçlar yoksa sözleşmeye göre yaz.
KORU/YAPMA: ARCH GATE (play/kampanya görünmez); mevcut Ziyaret Planlama / Ziyaret Yürütme / Planlanan Ziyaretler sayfalarına dokunma (yalnız menü ekleme); yeni JS kütüphanesi yok; etiketler referans verisinden (Web'de sabit neden listesi YOK); oturum açık kullanıcı sekmesine mock/harness enjekte etme, kalıcı yazma yok.
DOĞRULA (E2): Web 826/0 tabanı · CRM dokunulmaz · mimari 27. Testler belge Acceptance 1–7 (durum eşlemesi, geri sayım, E2 kuralları ve hata metinleri saf fonksiyon olarak Node'da; vekil yolları/yetkileri; 7 dil dolu ve anahtar yankısı değil); sabotaj 1 (requiresNote yok sayılsın → kırmızı; geri al — `git checkout --` YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W2-WEB-a — visit workspace calendar (execute mode), detail panel, cancel / not done / reschedule dialogs" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, mockup'tan bilinçli sapmalar, BE-a/BE-b'ye bağımlı kısımlar, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — W2-BE-b E2 ACCEPTED (2026-10-09)
**Commit:** `b0f6febaf` (ajan `09060cded`, test dalına cherry-pick, çakışma yok). Push: test dalı.

**CT K13:**
- CRM 2506 → **2513/0/5** (+7); iki tam koşu da yeşil.
- Mimari: listesiz **27**. Yeni yazma komutu yok; mevcut seçim güncellemesi kullanılıyor.

**Kod okuması:**
- `PlanningDayPin.StartTime` (class-map otomatik alıyor; eski kayıt boş okunuyor).
- Doğrulama `PlanningDayPins` içinde:
  - 15 dk adım ve yalnız ziyaret sabiti → `pin_time_invalid`;
  - pencere dışı ya da öğle arası → `pin_time_outside_hours`; pencere = `PlanningDayBudget.WindowFor`.
- Saf `DayTimePins.Place` gün rotasının üstünde çalışıyor; iyileştirici değişmedi.
- Çakışma → `PinMove(..., pin_time_conflict)`.
- Gün değiştiren sabit o günde saatsiz kalıyor.

**Ajanın paket dışı düzeltmesi (CT kabul):**
- `RouteDay` iç ziyaret kimliği `Guid.NewGuid()` yerine artık listedeki sıradan üretiliyor.
- Neden: iyileştirici eşit dakikalı yolları bu kimlikle ayırıyordu, aynı gün koşudan koşuya farklı sırada çıkıyordu.
- Kimlik yalnız `RouteDay` içindeki eşlemede kullanılıyor, dışarı sızmıyor. 4E / 4G yerleşimini de kararlı kılıyor.

**Ajanın kararları (CT kabul, kullanıcıya bildirildi):**
- Saatli sabit, doktorun müsaitlik penceresini yok sayar (temsilcinin açık seçimi).
- Gün sonunu aşan geç saat `pin_time_conflict` olarak kayar.
- `pinnedTime` istenen saat, `startTime` gerçek saat.

**CT sabotajı:** 15 dk adım kontrolü kaldırıldı → 1 kırmızı (`A_pin_time_outside_the_working_hours_or_off_the_grid_…`). `.bak` kopyasından geri alındı, dosyaya touch yapıldı, tam koşu yeşil.

**Bekleyen:** W2-BE-a (`pinnedTime`'ı takvim okumasına taşıyacak), W2-WEB-a.

### §37 ek — CT düzeltmesi: `pin_time_past_day_end` (kullanıcı isteği, 2026-10-09)
- **Ne:** geçerli bir saatli sabitin ziyareti mesai bitişini aşıyorsa (ör. 17:45 + 30 dk) ziyaret erkene çekilir. Taşınanlar listesine artık `pin_time_conflict` değil, ayrı kod **`pin_time_past_day_end`** yazılır.
- **Kod:**
  - sabit `PlanningDayPins.PinTimePastDayEnd`;
  - motorda ayrım: istenen saat + ziyaret süresi > günün pencere bitişi.
- **Test:** `A_late_pin_whose_visit_would_end_after_the_day_moves_earlier_with_pin_time_past_day_end`.
- **CT sabotajı:** ayrım kaldırıldı (hep `pin_time_conflict`) → 1 kırmızı. Geri alındı, touch yapıldı; tam tur yeşil.
- CRM **2514/0/5**, mimari 27.
- **W2-WEB-b için:** bu kodun kullanıcı metni 7 dilde eklenecek ("Ziyaret mesai bitişini aşıyor, erkene alındı").
- **Mobil notu:** yeni taşıma nedeni `pin_time_past_day_end`.

---

## Sözleşme eki (W2-BE-a sonrası, 2026-10-09) — WEB-a / WEB-b / mobil için BAĞLAYICI
- **Yapılamadı / ertele iki adım:**
  1. `POST api/crm/visit-report/outcome` (`executionOutcome` missed | rescheduled, `reasonCode`, `reasonNote`, ertelemede `rescheduleToDate`) → taslak.
  2. `POST api/crm/visit-report` + **`executionOutcome: "missed" | "rescheduled"`** → kesinleşir. Ertelemede yeni ziyaret bu adımda oluşur.

  Web penceresi tek "Kaydet" düğmesiyle iki çağrıyı sırayla yapar. İlki başarısızsa ikincisi çağrılmaz.
- **Ek hata kodları:**
  - `visit_reason_note_too_long`;
  - `visit_reason_applies_to_invalid`;
  - `reference_data_unavailable` (503, "neden listesi okunamadı, tekrar deneyin");
  - eski biçim hatası `visit_report_reschedule_date_invalid` (tarih biçimi bozuk).
- **Ağ geçidi:** `/api/crm/visit-workspace/{everything}` (GET, OPTIONS) rotası CT tarafından eklendi.
- **`pinnedTime`:** W2-BE-b birleştikten sonra CT takvim okumasına bağladı (yazılmış ziyarette o günün saatli sabiti, taslakta önizleme alanı).

## §37 CT kabul — W2-BE-a E2 ACCEPTED (2026-10-09)
**Commit:** `9869f6933` (ajan `8cdbdfafe`, test dalına cherry-pick, BE-b ile çakışma yok) + CT bağlama / rota commit'i. Push: test dalı.

**CT K13:**
- CRM 2514 → **2542/0/5**: ajandan +27, CT'den +1 (`A6b_…pinned_time…`).
- Platform katalog / tüketilebilir set testleri 24/0. Ajan, kendi değişikliğinden önce de aynı 14 Platform testinin (GSKU / Market, Mongo) kırmızı olduğunu ayrı çalışma ağacında gösterdi.
- Web 826/0.
- Mimari: listesiz **27** (yeni yazma komutu yok; mevcut komutlar genişledi).
- Ağ geçidi 86/1. Kırmızı `EveryRoute_DownstreamPortIsInKnownServiceSet`: Satın Alma portu 5065 bilinen port listesinde yok. Önceden de vardı; eklenen rota 5061 (CRM).

**Kod okuması:**
- Neden seti (`visit-outcome-reason`, tenant, 8 + 2 pasif, 7 dil) referans verisinden doğrulanıyor; sabit liste kullanılmıyor.
- İptal: geçmiş gün 409, kod + not.
- Erteleme: gönderimde yeni ziyaret; `IVisitRescheduleUnitOfWork` (işlem ya da telafi); idempotent; düzeltmede tarih kilidi.
- Plan dışı ziyaret yalnız bugün.
- Birleşik takvim (W1 okuması + önizleme kaynağı `IWorkspacePlanPreviewSource`).

**Ajanın paket dışı kararı (CT kabul):**
- missed / rescheduled bugün yalnız taslak olarak kaydediliyordu, kesinleştirme yolu yoktu.
- Yeni komut açmak yerine mevcut gönderim `executionOutcome` ile genişletildi; iki adımlı akış oldu.
- Sözleşme eki yukarıda; WEB-a ajanına iletildi.

**CT işleri:**
1. `pinnedTime` bağlandı: yazılmış ziyarette günün saatli sabiti, taslakta önizleme alanı. Test `A6b`.
2. Ağ geçidi rotası `/api/crm/visit-workspace/{everything}` (GET, OPTIONS) eklendi; ajan korumalı yol olduğu için dokunmamıştı.

**CT sabotajları** (ikisi birlikte, 2 kırmızı):
- iptalde geçmiş gün kontrolü kapatıldı → `A3_a_past_day_is_409…`;
- `pinnedTime` bağlantısı kaldırıldı → `A6b`.

Geri alındı, touch yapıldı; tam tur yeşil.

**Bilinen sınırlar (ajan bildirdi):**
- `holidayName` null;
- taslak kaynak `route-plan` olarak dönüyor;
- tenant seti yalnız `CatalogLoad:TenantId` (97c5) için yükleniyor; başka kiracıda 503.

**Sıradaki:** fleet yeniden başlatılınca canlı kontrol: set oluştu mu, `reasons`, `calendar`, `reschedule-options`.

### Sözleşme eki 2 — iki adımlı "Kaydet" davranışı (BE-a ajanı doğruladı, 2026-10-09)
1. **Yarıda kalan kayıt güvenle tekrarlanır.** 1. adım geçip 2. adım düşerse (ağ, 503) taslak kalır. Yeniden "Kaydet" iki çağrıyı tekrar yapar: 1. adım taslağın üzerine yazar, 2. adım kesinleştirir.
2. **Çift tıklama ikinci ziyaret açmaz.** 2. adım 60 dk içinde tekrar çağrılırsa hata dönmez ve yeni ziyaret oluşmaz. 60 dk'dan sonra `409 visit_report_edit_window_closed` döner.
3. **Erteleme tarihi iki adımda da kontrol edilir.** Hata kodu her iki adımda `visit_reschedule_date_invalid`.
4. **Not:**
   - sınır 500 (`maxNoteLength`, sözleşme ucu); aşılırsa `visit_reason_note_too_long`;
   - Web yalnız `reasonNote` gönderir (`rescheduleNotes` göndermez).
5. **Durum hataları:**
   - 48 sa son tarih geçtiyse iki adım da `409 visit_report_deadline_passed` (read-all muaf);
   - taslak yoksa ya da sonucu farklıysa 2. adım `409 visit_report_invalid_transition` döner.

### §37 ek — W2-BE-a canlı E4 (2026-10-09, CT ayrı sekme, yalnız okuma) + CT düzeltmeleri
- **Referans seti:** `visit-outcome-reason` 97c5'te yayımlanmış, 10 değer (8 aktif + 2 pasif), 7 dil etiketi dolu (Mongo okuması).
- **`contract`:** 10 workStatus (draft dahil), 13 hata kodu, `maxNoteLength` 500, `rescheduleOptionDays` 8.
- **`reasons`:**
  - `appliesTo=reschedule&lang=tr` → 7 neden (`target_inactive` yok, `other*`);
  - `cancel&lang=ar` → Arapça etiketler;
  - `xyz` → `400 visit_reason_applies_to_invalid`.
- **`calendar`** (5–25 Eki):
  - 165 ziyaret: 41. hafta 33 `missed` + 21 `today`, 12 Eki 3 `cancelled`, 42 ve 43. hafta 54'er `draft` (ürün adları dolu);
  - 26 Eki – 1 Kas: 29 Eki `isHoliday`, hafta kapasitesi 1840 (4 gün);
  - 60 günlük pencere → 400.
- **Canlı bulgu → CT düzeltmesi (commit aşağıda):**
  1. `weeks[].weekNumber` penceredeki sıra numarasıydı (1, 2, 3…). Artık ISO hafta numarası (44). Test: `A6b` (42, 43).
  2. `reschedule-options` taslak haftaların yükünü saymıyordu (12–21 Eki günleri 0 / 460 görünüyordu, oysa taslak plan dolu). Artık taslak hafta gününde önizlemenin dakikası ve ziyaret sayısı geliyor (takvim okumasıyla aynı kaynak). Test: `A7b`.
- **CT sabotajları:** taslak yükü kapatıldı + hafta numarası 1 yapıldı → 2 kırmızı (`A7b`, `A6b`). Geri alındı, touch yapıldı.
- CRM **2543/0/5** (iki tam tur).
- **Bilinen sınır:** `holidayName` boş (çalışma takvimi okuması adı taşımıyor). Web "Tatil" yazar.
