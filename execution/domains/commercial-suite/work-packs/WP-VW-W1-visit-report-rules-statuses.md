# WORK PACKAGE — WP-VW-W1 · Ziyaret raporu kuralları, ziyaret durumları, takvim adları, rapor yetkileri (backend)

> **CT (SoR), 2026-10-09.** [Ziyaret Çalışma Alanı yol haritası](ROADMAP-visit-workspace.md) W1. Ertelenmiş [WP-VP-4K](WP-VP-4K-visit-report-rules-calendar-names.md) bu paketin içine alındı; 4K ayrıca dağıtılmaz.
>
> **Kaynaklar:**
> - [CT değerlendirmesi (Android)](mobile/2026-10-08-visit-planning-contract/CT-REVIEW-android-visit-report-status-2026-10-08.md);
> - mockup durum tablosu (`mockups/visit-workspace/decoded/01-takvim.html`, `00-demo-veri.js` → `ST`).
>
> **Kullanıcı kararları:**
> - 2026-10-08:
>   - iptal edilmiş ziyaret düzenlenemez ve raporlanamaz;
>   - "bugün" UTC;
>   - rapor son tarihi ziyaretten sonra 48 saat;
>   - saha temsilcisi rolünü kullanıcı açar ve yetkileri ekrandan verir.
> - 2026-10-09:
>   - Faz 8 en sonda;
>   - K-W1 = A: erteleme yeni ziyaret oluşturur, ama bu W2'nin işi.
>
> **Kapsam:** CRM servisi + Web vekilinin yetki anahtarı. **Web ekranı değişmez.** Takvim ekranı W2'de gelir; bu paket ona ve mobile durum sözleşmesini verir.
>
> **Çalışma yeri:** worktree `C:\tmp\vw-w1`, dal `wp/vw-w1` (test dalının başından). Commit dala yapılır, push YOK.

## NE

### 1–7. 4K maddeleri (aynen geçerli)
[WP-VP-4K](WP-VP-4K-visit-report-rules-calendar-names.md) §NE madde 1–7 bu paketin parçasıdır:
1. **Takvim adları ve iptal nedeni:** takvim öğesine `cancellationReason` ve `plannedContent[].productName` (önce anlık görüntü, yoksa tek toplu MDM çağrısı; hata vermez).
2. **T-1:** `account-contact-link` hedefinin adı = doktor.
3. **Raporu yazan = çağıran.** read-all istisna; başkası adına → `403 resource_not_caller`.
4. **İptal edilmiş ziyaret:**
   - güncelleme → `409 planned_visit_invalid_transition`;
   - sonuç kaydı ve gönderim → `409 visit_report_plan_cancelled` (yeni);
   - daha önce gönderilmiş raporun düzeltmesi serbest.
5. **Rapor son tarihi** = ziyaret günü sonu (UTC) + **48 saat** (`VisitReportLimits.ReportDeadlineHours = 48`):
   - sonrasında sonuç kaydı ve gönderim → `409 visit_report_deadline_passed`;
   - read-all sahibi muaf;
   - düzeltme sınırsız;
   - `contract.reportDeadlineHours`, takvim `reportDeadline`.
6. **Takvim aralık hatası** → `visit_report_calendar_range_invalid`.
7. **F-RBAC:** rapor uçları `crm.visit-report.read / record / amend`; `crm.territory.*` yedeği kalkar. Web vekili ile `CanRecord` / `CanAmend` aynı anahtarları kullanır. Ön koşul raporu yalnız okumayla çıkarılır; saha temsilcisi yetki listesi uçlardaki gerçek kontrollerle doğrulanır.

**Madde 5 ile 8 birlikte okunur:**
- 48 saatlik pencere, sonucun **her türü** için geçerli: yapıldı (`completed`), yapılamadı (`missed`), ertelendi (`rescheduled`).
- Yani "kaçırıldı" ziyarette temsilcinin 48 saati vardır: yapılamadı ya da ertele der. Süre geçince ziyaret kilitlenir.

### 8. Ziyaret iş durumu (yeni okuma alanı — mockup durum tablosu)
Takvim ve ayrıntı tek bir **türetilmiş durum** okusun. Durum kayıt edilmez; plan + rapor + şimdiki zamandan hesaplanır. Web takvimi (W2) ve mobil aynı kodu okur.

- **Tek yer:** `VisitWorkStatus.Derive(plannedVisit, report?, now)`, saf ve `TimeProvider` ile test edilebilir.
- **Yeni alanlar:**
  - takvim öğesinde (`GET api/crm/visit-report/calendar`) ve planlanan ziyaret ayrıntısında `workStatus`;
  - `reportDeadline`, madde 5 ile aynı;
  - `managerAttention` (bool).
- **Durumlar** (kod İngilizce, sıra öncelik sırasıdır; ilk tutan kazanır):

| `workStatus` | Ne zaman | Mockup karşılığı | Temsilci ne yapabilir |
|---|---|---|---|
| `cancelled` | plan iptal edilmiş (`week_reopened` dahil) | iptal | hiçbir şey (madde 4) |
| `not_done` | gönderilmiş rapor, sonuç `missed` | yapılamadı | düzelt (amend) |
| `rescheduled` | gönderilmiş rapor, sonuç `rescheduled` | ertelendi | düzelt (amend) |
| `reported` | gönderilmiş rapor, sonuç `completed` | raporlandı | 60 dk içinde yeniden gönder, sonra düzelt |
| `expired` | gönderilmiş rapor yok **ve** son tarih geçti | süre doldu | kilitli (madde 5); yalnız read-all |
| `report_missing` | sonucu `completed` olan taslak rapor var, gönderilmemiş, son tarih geçmedi | rapor eksik | raporu gönder |
| `missed` | ziyaret günü geçti, hiç sonuç yok, son tarih geçmedi | kaçırıldı | yapılamadı ya da ertele (48 sa içinde) |
| `today` | ziyaret günü = bugün (UTC), sonuç yok | bugün | sonuç gir |
| `planned` | ziyaret günü gelecekte | planlı | (erken giriş `visit_not_yet_due`) |

- **Notlar:**
  - Taslak rapor varsa ve sonucu `missed` ya da `rescheduled` ise, gönderilene kadar durum `missed` kalır (son tarih geçince `expired`).
  - `in_progress` ("devam ediyor") bu pakette **yok**. W3'te ziyaret başlat / bitir kaydıyla gelir, sıralamada `today`'den önce yer alır. Kod buna yer bırakır (enum'a bilinçli olarak eklenmez).
- **`managerAttention = true`:** durum `expired` olduğunda, yani son tarih geçti ve rapor yok. Mockup'taki "kilit + yöneticiye bildirim".
  - **Bu pakette yalnız bayrak var.** E-posta W5'te bu bayrağı kaynak alır.
  - Bayrak yazılmaz, okunurken hesaplanır.
- **Takvim süzgeci (ek, isteğe bağlı parametre):** `?workStatus=missed,expired` (virgüllü). Bilinmeyen değer → `400 visit_report_work_status_invalid`. Parametre verilmezse eski davranış sürer.
- **Sözleşme:** `GET api/crm/visit-report/contract` yanıtına `workStatuses[]` (kodlar, öncelik sırasıyla). Mobil bunu sabit liste olarak kullanır.

## KORU / YAPMA
- **Yeni yazma komutu YOK.** Mevcut komutlara kural eklenir, mimari test sayısı **27** kalır.
  - Faz 8 en sonda (kullanıcı kararı). Bu pakette denetim işi yok.
- "Bugün" UTC. `visit_not_yet_due`, 60 dk pencere ve düzeltme akışı aynen kalır.
- **`workStatus` kaydedilmez:** göç, alan, indeks yok. Okurken hesaplanır.
- Mobil sözleşmesine yalnız **ek alan, yeni hata kodu ve isteğe bağlı parametre** gelir; mevcut alan adları değişmez.
- **Erteleme hâlâ yalnız rapor sonucudur.** Yeni ziyaret oluşturma (K-W1 = A) W2'nin işi, burada yapılmaz.
- Neden kategorileri (referans seti) de W2'nin işi.
- Göç, seed, grant ve indeks YOK. Canlı veriye yazma yok. Kiracı sınırı (TenantId) korunur. ARCH GATE: temsilci play / kampanya görmez.
- Web ekranı değişmez, yalnız yetki anahtarı. Ziyaret Yürütme sayfası durum alanını göstermek zorunda değil (W2 takvimi gösterecek).

## Acceptance
- **4K Acceptance 1–7** aynen (belge §Acceptance).
- **8a — durum türetme** (saf testler, saat sabit):
  - tablodaki her satır için en az bir örnek;
  - öncelik çakışmaları:
    - iptal edilmiş + raporlu → `cancelled`;
    - son tarih geçmiş + taslak `completed` → `expired`;
    - gönderilmiş rapor + son tarih geçmiş → `reported` (kilit yalnız raporsuza);
    - taslak `missed` → `missed`, sonra `expired`.
- **8b — sınır:** Perşembe ziyareti:
  - Cuma 10:00Z → `missed`;
  - Cumartesi 23:59:59Z → `missed`;
  - Pazar 00:00:00Z → `expired` + `managerAttention = true`;
  - aynı anda sonuç kaydı → `409 visit_report_deadline_passed`. Durum ile kural aynı son tarihi okur; tek yardımcı kullanılır.
- **8c — uç düzeyi:** takvim ve ayrıntı yanıtında `workStatus`, `reportDeadline` ve `managerAttention` var. `?workStatus=missed` yalnız kaçırılanları getiriyor; bozuk değer → `400 visit_report_work_status_invalid`; contract'ta `workStatuses[]` var.
- **Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
  - raporlayanı yine istekten al → 4K test 3 kırmızı;
  - son tarih kontrolünü kaldır → 4K test 5 kırmızı;
  - yedek yetkiyi geri koy → 4K test 7 kırmızı;
  - durumda son tarihi 24 saate çek (kuraldan ayrı bir sabit kullan) → 8b kırmızı.
- **Mimari:** 27, artmaz.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W1 · Ziyaret raporu kuralları, ziyaret durumları, takvim adları, rapor yetkileri (backend)
Repository: C:\tmp\vw-w1 (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vw-w1 · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W1-visit-report-rules-statuses.md — önce oku. Madde 1–7'nin ayrıntısı ve Acceptance 1–7: WP-VP-4K-visit-report-rules-calendar-names.md (o paket buraya alındı, ayrıca yapılmaz). Bağlam: ROADMAP-visit-workspace.md (W1, durum tablosu), mobile/2026-10-08-visit-planning-contract/CT-REVIEW-android-visit-report-status-2026-10-08.md, WP-VP-2 §37 (ICallerScope/VisitOwnership, read-all), WP-VP-4G §37 (IProductNameReader, ProductName anlık görüntüsü), WP-E2E-FIX-1 §37 (visit_not_yet_due). Kod: services/Diten.CrmService/src/**/Features/{VisitReport,PlannedVisit}/** · Api/Controllers/CRM/VisitReportController.cs · frontend/Diten.Web/Controllers/CRM/CrmVisitExecutionController.cs.
NE: (1–7) 4K: takvim cancellationReason + plannedContent[].productName (anlık görüntü > tek toplu MDM, fail-open); T-1 bağlantı hedefi adı = doktor; raporlayan = çağıran (read-all istisna, 403 resource_not_caller); iptal edilmiş ziyaret: güncelleme 409, outcome/submit 409 visit_report_plan_cancelled, önceden gönderilmiş raporun amend'i serbest; son tarih = plannedDate günü sonu UTC + 48 sa (VisitReportLimits.ReportDeadlineHours=48) → outcome (completed/missed/rescheduled hepsi) ve submit 409 visit_report_deadline_passed, read-all muaf, amend sınırsız, contract reportDeadlineHours, takvim reportDeadline; takvim aralık hatası visit_report_calendar_range_invalid; F-RBAC crm.visit-report.read/record/amend (crm.territory.* yedeği kalkar, Web vekili + CanRecord/CanAmend aynı), ön koşul raporu YALNIZ OKUMA, saha temsilcisi yetki listesi HasPermission taramasıyla. (8) türetilmiş ziyaret iş durumu: VisitWorkStatus.Derive(plan, report?, now) saf + TimeProvider; öncelik sırası cancelled > not_done > rescheduled > reported > expired > report_missing > missed > today > planned (belgedeki tablo); managerAttention = expired; takvim + planlanan ziyaret ayrıntısında workStatus/reportDeadline/managerAttention; takvime isteğe bağlı ?workStatus=a,b (bozuk → 400 visit_report_work_status_invalid); contract workStatuses[]; durum ve kural AYNI son tarih yardımcısını kullanır; in_progress YOK (W3, yer bırak).
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz 27; Faz 8 en sonda); workStatus KAYDEDİLMEZ (okurken hesaplanır; göç/alan/indeks yok); erteleme = yalnız rapor sonucu (yeni ziyaret oluşturma W2), neden kategorileri W2; "bugün" UTC; visit_not_yet_due / 60 dk / amend aynen; mobil yalnız ek alan + yeni kod + isteğe bağlı parametre; seed/grant yok; canlı veriye yazma yok; TenantId; ARCH GATE; Web ekranı değişmez (yalnız yetki anahtarı).
DOĞRULA (E2): CRM 2470/0/5 tabanı (ContactLocationPiiHardeningTests.PiiMasking_… bilinen kararsız; tekrar koşunca geçer) · Web 822/0 (vekil anahtarı değiştiği için koş) · mimari 27. Testler belge Acceptance (4K 1–7 + 8a/8b/8c); sabotaj 4 (kırmızı kanıtla, geri al — `git checkout --` kullanma; geri alınca dosyaya touch). dotnet test -o kullanırsan çıktı klasörü REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W1 — visit work status (missed/expired), report deadline 48h, cancelled locked, reporter = caller, calendar names, visit-report RBAC" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, ön koşul raporu (anahtarlar Auth kataloğunda / 97c5 Admin rolünde var mı), saha temsilcisi yetki listesi, mobil için yeni alan / kodlar / durum listesi, elle denenecekler. §22 TÜRKÇE. K13.
```
