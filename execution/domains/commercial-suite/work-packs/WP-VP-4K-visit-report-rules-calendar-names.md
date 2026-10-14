# WORK PACKAGE — WP-VP-4K · Ziyaret raporu kuralları, takvim adları ve rapor yetkileri (backend)

> ⤷ **2026-10-09: BU PAKET AYRICA DAĞITILMAZ** — içeriği [WP-VW-W1](WP-VW-W1-visit-report-rules-statuses.md) içine alındı (madde 1–7 + Acceptance 1–7 orada referansla geçerli).

> **CT (SoR), 2026-10-08.** Android durum raporu + CT kod doğrulaması: [CT değerlendirmesi](mobile/2026-10-08-visit-planning-contract/CT-REVIEW-android-visit-report-status-2026-10-08.md).
> - **Kullanıcı kararları (2026-10-08):**
>   - **İptal edilmiş planlanan ziyaret:** düzenlenemez ve rapor girilemez.
>   - **"Bugün":** UTC kalır (değişiklik yok).
>   - **Rapor son tarihi:** ziyaretten sonra **48 saat**.
>   - **Saha temsilcisi rolü:** kullanıcı rolü açar ve yetkileri ekrandan verir; backend rapor uçlarını geçici yedek yetkiden gerçek `crm.visit-report.*` yetkilerine taşır.
> - **Kapsam:** CRM servisi (+ gerekirse Web vekilinin yetki kontrolü). WP-VP-4J (Web) ile paralel.
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4k`, dal `wp/vp-4k` (test dalı başından). Commit dala, push YOK.

## NE

### 1. Takvim: ürün adı ve iptal nedeni (mobil engeli)
- **`VisitCalendarItemDto` (`GET api/crm/visit-report/calendar`):**
  - ek alan `cancellationReason` (planlanan ziyaretin iptal nedeni; `week_reopened` dahil);
  - `plannedContent[]` öğesine ek alan `productName`.
- **Ad kaynağı:** önce onayda dondurulmuş anlık görüntü (`PlannedVisitContentItem.ProductName`, 4G). Yoksa okuma anında `IProductNameReader` ile toplu tek çağrı (4G kuralı).
  - Hata vermez: MDM yoksa null + 200.
  - İstek başına tek MDM çağrısı.

### 2. T-1 — "işyerindeki doktor" hedefinin adı
- `VisitTargetNameReader.For`: `account-contact-link` hedefini kurumlarda bağlantı kimliğiyle arıyor → ad boş.
- **Doğru davranış:** bağlantı hedefinde **ad = doktor** (`ContactId`), `account = AccountId`; pasiflik doktordan.
- Takvim, planlanan ziyaret listesi ve ayrıntı aynı okuyucuyu kullanıyor; hepsi düzelir.

### 3. Raporu yazan = oturumdaki kişi (güvenlik)
- `RecordVisitOutcome`, `SubmitVisitReport`, `AmendVisitReport` bugün `ReportedByResourceId`'i **istekten** alıyor.
- **Kural:** raporlayan her zaman **çağıran** (`ICallerScope` / `VisitOwnership.ResolveWriteResource`, B-1 kuralının aynısı).
  - Yalnız `crm.planned-visit.read-all` sahibi başkası adına yazabilir.
  - Başkası adına deneme → `403 resource_not_caller` (mevcut kod).
  - İstekteki alan geriye uyum için kabul edilir; çağıranla aynı değilse ve read-all yoksa 403.

### 4. İptal edilmiş planlanan ziyaret (kullanıcı kararı)
- **Güncelleme:** `UpdatePlannedVisitHandler` iptal edilmiş planı reddeder → `409 planned_visit_invalid_transition` (mevcut kod; mesaj "Cancelled visit cannot be modified"). Arşiv kuralı aynen.
- **Rapor:** sonuç kaydı (`outcome`, her sonuç) ve gönderim (`submit`), planı **iptal edilmiş** ziyarette reddedilir → **yeni** `409 visit_report_plan_cancelled`.
- **Düzeltme:** daha önce gönderilmiş bir rapor, plan sonradan iptal edilse de **düzeltilebilir**. Geçmişin düzeltilmesi; denetim kuralı.
- Hafta yeniden açılınca raporu olan ziyaret zaten iptal edilmiyor (değişmez).

### 5. Rapor son tarihi: 48 saat (kullanıcı kararı)
- **Son tarih** = ziyaret gününün sonu (UTC, `plannedDate` 23:59:59) **+ 48 saat**.
  - Örnek: Perşembe ziyareti → Cumartesi 23:59:59 UTC'ye kadar.
  - "Bugün" UTC kuralı aynen (kullanıcı kararı).
- **Kapsam:** son tarihten sonra ilk sonuç kaydı (`outcome`: completed / missed / rescheduled) ve gönderim (`submit`) → **yeni** `409 visit_report_deadline_passed`.
  - Taslak rapor varsa ve son tarih geçtiyse gönderim de reddedilir.
- **Dokunulmayanlar:**
  - **Düzeltme** (`amend`) sınırsız kalır (gönderilmiş raporun düzeltilmesi).
  - 60 dakikalık yeniden gönderme penceresi aynen.
  - `visit_not_yet_due` (erken giriş) aynen.
- **CT varsayılanı:** `crm.planned-visit.read-all` sahibi (yönetici) son tarihten sonra da kaydedebilir. Temsilci geç kaldığında yönetici düzeltebilsin; kullanıcıya bildirilecek.
- **Sözleşme:** `GET api/crm/visit-report/contract` yanıtına `reportDeadlineHours = 48` (ek alan). Takvim öğesine ek alan `reportDeadline` (ISO zaman, UTC) — mobil geri sayım / kilit için.
- **Sabit:** `VisitReportLimits.ReportDeadlineHours = 48` (tek yer; testle korunur).

### 6. Takvim hata kodu
- Eksik / bozuk tarih aralığında takvim bugün `400 visit_report_reschedule_date_invalid` dönüyor (yanıltıcı ad).
- Yeni kod `visit_report_calendar_range_invalid`. Eski kod bu uçta artık dönmez; mobil notuna yazılacak.

### 7. F-RBAC: rapor uçları gerçek yetkiye
- `VisitReportController`:
  - okuma uçları (`contract`, `calendar`, liste, ayrıntı) → `crm.visit-report.read`;
  - `outcome` ve gönderim → `crm.visit-report.record`;
  - `amend` → `crm.visit-report.amend`.
  - `ReadFallback` (`crm.territory.read`) / `ManageFallback` (`crm.territory.model.manage`) **kaldırılır**.
- Web Ziyaret Yürütme vekili (`CrmVisitExecutionController`) ve sayfa modeli (`CanRecord` / `CanAmend`) aynı anahtarları kullanır; yedek varsa kalkar.
- **Ön koşul raporu (canlı veriye yazma YOK, yalnız okuma):**
  - bu üç anahtar Auth kataloğunda var mı;
  - 97c5 **Admin** rolünde var mı (modül eşitlemesi / manifest).
  - Yoksa rapora açıkça yaz: kullanıcı yetkileri vermeden dağıtım yapılırsa Ziyaret Yürütme Admin'de de 403 olur.
- **Saha temsilcisi rolü:** kullanıcı ekrandan açar. Rapora önerilen yetki listesi:
  - `crm.planned-visit.read` / `.manage` / `.confirm`;
  - `crm.visit-plan.read` / `.generate` / `.apply`;
  - `crm.visit-report.read` / `.record` / `.amend`;
  - `crm.account.read`, `crm.contact.read`;
  - `mdm.global-products.read`.
  - Ajan bu listeyi uçlardaki gerçek `HasPermission` taramasıyla doğrular (eksik / fazla yazmasın).

## KORU / YAPMA
- **Yeni yazma komutu YOK** (listesiz 27). Mevcut komutlara kural eklenir; mimari test sayısı artmaz.
- "Bugün" UTC (değişmez). `visit_not_yet_due`, 60 dk pencere, düzeltme akışı aynen.
- Mobil sözleşmesi yalnız **ek alan + yeni hata kodu**. Mevcut alan adları değişmez.
- Göç / seed / grant / indeks YOK. Canlı veriye yazma yok. Kiracı sınırı. ARCH GATE.
- Web ekranı değişikliği yok (yalnız yetki anahtarı). İptal edilmiş ziyaretin Web'de salt okunur görünmesi Faz 6 / Ziyaret Yürütme işi; bu pakette yalnız backend reddi.

## Acceptance
- Testler (üretim kodu üzerinde):
  1. Takvim: `cancellationReason` (`week_reopened` / elle iptal); `plannedContent[].productName` (anlık görüntü > MDM; MDM yoksa null + 200; tek çağrı).
  2. T-1: `account-contact-link` hedefinde `targetDisplayName` = doktor adı (takvim + liste + ayrıntı).
  3. Raporlayan: istekte başka kaynak → 403; read-all ile serbest; alan yoksa çağıran.
  4. İptal edilmiş ziyaret: güncelleme 409; sonuç / gönderim `visit_report_plan_cancelled`; daha önce gönderilmiş raporun düzeltmesi serbest.
  5. Son tarih: Perşembe ziyareti Cumartesi 23:59:59Z'ye kadar kabul, Pazar 00:00:00Z red (`visit_report_deadline_passed`); read-all muaf; düzeltme sınırsız; contract `reportDeadlineHours = 48`; takvim `reportDeadline`. Saat sabitlenmiş (`TimeProvider`).
  6. Takvim aralık hatası yeni kodla.
  7. Rapor uçları `crm.visit-report.*` ister; yedek anahtarla 403 (izin testleri / öznitelik taraması).
- **Sabotajlar (kırmızı kanıtla, geri al):**
  - raporlayanı yine istekten al → test 3 kırmızı;
  - son tarih kontrolünü kaldır → test 5 kırmızı;
  - yedek yetkiyi geri koy → test 7 kırmızı.
- Mimari: 27 (artmaz).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-VP-4J ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4K · Ziyaret raporu kuralları, takvim adları ve rapor yetkileri (backend)
Repository: C:\tmp\vp-4k (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4k · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4K-visit-report-rules-calendar-names.md — önce oku (madde 1–7 + kullanıcı kararları). Bağlam: mobile/2026-10-08-visit-planning-contract/CT-REVIEW-android-visit-report-status-2026-10-08.md; WP-VP-2 §37 (ICallerScope/VisitOwnership, read-all), WP-VP-4G §37 (IProductNameReader, ProductName anlık görüntüsü), WP-E2E-FIX-1 §37 (visit_not_yet_due). Kod: services/Diten.CrmService/src/**/Features/{VisitReport,PlannedVisit}/** · Api/Controllers/CRM/VisitReportController.cs · frontend/Diten.Web/Controllers/CRM/CrmVisitExecutionController.cs.
NE: (1) takvim öğesine cancellationReason + plannedContent[].productName (anlık görüntü > toplu tek MDM çağrısı, fail-open). (2) T-1: account-contact-link hedefinin adı = doktor (VisitTargetNameReader.For). (3) raporlayan = çağıran (ResolveWriteResource; read-all istisna; başkası → 403 resource_not_caller). (4) iptal edilmiş planlanan ziyaret: güncelleme 409 planned_visit_invalid_transition; outcome/submit 409 visit_report_plan_cancelled (yeni kod); önceden gönderilmiş raporun amend'i serbest. (5) rapor son tarihi = plannedDate günü sonu (UTC) + 48 sa (VisitReportLimits.ReportDeadlineHours=48): sonrasında outcome/submit 409 visit_report_deadline_passed; read-all muaf (CT varsayılanı); amend sınırsız; contract reportDeadlineHours; takvim reportDeadline; TimeProvider ile test. (6) takvim aralık hatası visit_report_calendar_range_invalid. (7) F-RBAC: rapor uçları crm.visit-report.read/record/amend, crm.territory.* yedeği kalkar; Web vekili + CanRecord/CanAmend aynı anahtarlar; ön koşul raporu: anahtarlar Auth kataloğunda ve 97c5 Admin rolünde var mı (YALNIZ OKUMA); saha temsilcisi için önerilen yetki listesini uçların HasPermission taramasıyla doğrula.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz 27); "bugün" UTC; visit_not_yet_due / 60 dk pencere / amend aynen; mobil yalnız ek alan + yeni hata kodu; göç/seed/grant/indeks yok; canlı veriye yazma yok; TenantId; ARCH GATE; Web ekranı değişmez (yalnız yetki anahtarı).
DOĞRULA (E2): CRM (2457/0/5 tabanı; bilinen kararsız testler olabilir) · Web 810/0 (yalnız vekil anahtarı değişirse koş) · mimari 27. Testler belge Acceptance 1–7; sabotaj 3 (kırmızı kanıtla, geri al — commit'lenmemiş başka iş varsa `git checkout --` kullanma). dotnet test -o kullanırsan çıktı klasörü REPO İÇİNDE.
Commit: "feat(crm): WP-VP-4K — report deadline 48h, cancelled visits locked, reporter = caller, calendar names + cancellation reason, visit-report RBAC keys" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, ön koşul raporu (anahtarlar / Admin rolü), saha temsilcisi yetki listesi, mobil için yeni alan / kodlar, elle denenecekler. §22 TÜRKÇE. K13.
```
