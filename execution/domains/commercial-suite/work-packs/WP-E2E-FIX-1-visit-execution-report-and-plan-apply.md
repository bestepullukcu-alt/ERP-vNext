# WORK PACKAGE — WP-E2E-FIX-1 · Ziyaret Yürütme raporu ("ne sunacağım" + plandan yolculuk / aşama) + plan uygula / düzenle

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** [E2E TUTUKON](E2E-TUTUKON-content-to-visit-plan.md) bulguları E9-B1 (ön koşul kısmı), E9-B2…B6, E7-B4, E8-B1 · [yol haritası](ROADMAP-visit-planning.md) Faz E2E → E2E-FIX.
> - **Kullanıcı:** "test bulgularını paketleyebilirsin" (2026-10-07).
> - **Kapsam:** CRM (ziyaret raporu takvim okuması + iki mevcut yazma komutunda tarih kuralı) + Web (Ziyaret Yürütme sayfası, Ziyaret Planlama form / detay).
> - **Kapsam dışı:** yolculuk ilerlemesini **yazan** uç (E9-B1'in kendisi) → **SB-3c** (ayrı paket; `JourneyProgress.Advance` + `UpsertAsync` bugün hiçbir yerden çağrılmıyor, tasarım gereği). Bu paket yalnız ön koşulu sağlar: rapor plandaki yolculuk / aşama kimliklerini taşır.
>
> **Çalışma yeri:** worktree `C:\tmp\e2e-fix-1`, dal `wp/e2e-fix-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel paketler:** WP-E2E-FIX-2 (bilgi zinciri ekranları) ve WP-E2E-FIX-3 (segment / oyun / sıklık) ayrı dallarda. Dosya çakışması beklenmez; `SharedResource.*.resx`'e dokunma (gerekirse görünüm resx'i).

## Bağlam (CT kod okuması, 2026-10-07)
Kısaltmalar: **VE** = `frontend/Diten.Web/wwwroot/assets/js/CRM/VisitExecution/visit-execution.js` · **VR** = `services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitReport/` · **VP** = `frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/`.

| Bulgu | Kök (dosya:satır) |
|---|---|
| E9-B1 | `VE:232-249` `reportBody()` yalnız `stageCode`, `stageIndex`, `matchedPlan` gönderiyor. DTO'da alanlar hazır: `VR/VisitReportModels.cs:106-113` (`JourneyId`, `StageId`, `StageIndex`, `StageCode`, `MatchedPlan`, `JourneyDisplayName`, `StageDisplayName`); sunucu aynen kopyalıyor (`VR/VisitReportMapper.cs:50-62`). Takvim öğesinde `plannedJourneyId / plannedStageId / plannedStageIndex` var (`VisitReportModels.cs:88-90`), ama `renderCalendar` (`VE:94-116`) öğeleri saklamıyor, `openReport(id)` (`VE:179-189`) yalnız kimlik alıyor. |
| E9-B2 | `#ve-planned-content` `Views/CRM/VisitExecution/Index.cshtml:86`'da sabit "—"; `resetReportForm` (`VE:198`) yine "—" yazıyor; hiçbir kod doldurmuyor. Takvim DTO'su (`VisitReportModels.cs:76-99`) yalnız eski tekil `Content` kimliklerini taşıyor (`GetVisitCalendarHandler.cs:120-122`); `ContentItems` yok. Veri hazır: `PlannedVisit.ContentItems` (`Domain/Entities/PlannedVisit.cs:133`, öğe `:243-262`). Kart `VE:120` aşamayı `'#' + plannedStageIndex` yazıyor → "#0". |
| E9-B3 | `Index.cshtml:91-96` serbest metin `ve-actual-stage-code` + sayı `ve-actual-stage-index`. Aşama listesi için CRM ucu var: `Api/Controllers/CRM/ContentEngagementJourneysController.cs:111`; Web'de vekil `PlannedVisitsController.cs:242-246` ve `ContentEngagementJourneysController.cs:194-198`; `CrmVisitExecutionController.cs:71-103`'te yok. |
| E9-B4 | Kural `VR/VisitReportValidation.cs` ~144 `ValidateReportContent`: boş olmama + uzunluk; kod `visit_report_outcome_code_required` (`Contract/VisitReportErrorCodes.cs:16`). UI `errorOf` (`VE:283-288`) `errors[0]`'ı gösterir, değilse `L.actionFailed`. Kullanıcı "İşlem başarısız" gördü → yanıt büyük olasılıkla `errors` dizisi değil (ör. `[ApiController]` ProblemDetails) → **önce canlıda / testte yanıt şeklini doğrula**. `contract.outcomeCodes` (`VE:59-61`) bekleniyor ama `VisitReportVocabularyDto` (`Contract/VisitReportContract.cs:27-36`) bu alanı taşımıyor → datalist hep boş. Hiçbir serviste sonuç kodu referans kümesi yok. |
| E9-B5 | `RecordVisitOutcomeHandler.cs:54-97` ve `SubmitVisitReportHandler.cs:54-81` `plan.PlannedDate`'i bugünle hiç karşılaştırmıyor. |
| E9-B6 | `recordOutcome` (`VE:162-177`) `completed`'i onaysız gönderiyor (missed / rescheduled `window.prompt`). Kart `VE:141` `esc(it.executionOutcome)` ham kod. Başlık `Index.cshtml:6,17` `Localizer["PageTitle"]` = "Ziyaret Raporu" (`VisitExecutionIndex.tr.resx:7`), menü "Ziyaret Yürütme" (`Nav.Page.VISITEXECUTION`). |
| E7-B4 | `VP/form.js:203-207` `newId = (r.body && r.body.data) \|\| sessionId`; güncelleme `Response<bool>` döner (`PlanningSessionCommandHandlers.cs:180`) → `/Details/true` 404. |
| E8-B1 | `VP/details.js:1165-1175` (bağ `:1300`) `/apply`'ı onaysız gönderiyor. Başarı mesajı kodda var (`:1172`, yalnız `r.body.data` doğruysa) → canlıda neden çıkmadığını doğrula (`VisitPlanApplyResult` şekli `VisitPlanningModels.cs:118-123`). Salt okunur durum yüklemede bir kez (`details.js:21` `root.dataset.readOnly`; sunucu `VisitPlanningController.cs:119-126`); uygulamadan sonra `loadSession()` (`:75-95`) yalnız rozeti güncelliyor. |

Yardımcılar: `window.showConfirm(text, cb, {type, confirmButtonText})` (`Views/Shared/_GlobalConfirmation.cshtml:206`; örnek `CRM/KnowledgePaths/workspace-review.js:237`) · `window.showToast(msg, type)` (`_GlobalNotification.cshtml:118`).

## NE
### 1. (E9-B2) "Ne sunacağım" görünür
- **CRM:** takvim öğesine **ek** (geriye uyumlu) alan: `plannedContent` — planlanan ziyaretin `ContentItems` özetinden liste: `productId, productCode, role, journeyId, journeyCode, stageId, stageIndex, stageName, steps[{ title, type }]`. Eski alanlar (`plannedJourneyId` …) aynen kalır. `ContentItems` boşsa eski `Content` tekilinden tek öğe türet ya da boş liste.
- Takvim okuması sahiplik / kiracı kurallarını aynen korur; adımlar için ek sorgu yok (veri planlanan ziyaretin üzerinde).
- **Web kartı:** "#0" yerine ürün kodu çipi + aşama adı (ör. `TUTUKON · Farkındalık`). Birden çok ürün varsa çip listesi.
- **Web rapor formu:** "Planlanan içerik" bölümü ürün → aşama → adım başlıkları listesi. Boşsa "Bu ziyaret için planlanmış içerik yok".

### 2. (E9-B1 ön koşul + E9-B3) Gerçek aşama seçimle, plandan varsayılan
- Web takvim öğelerini bellekte tutar (`plannedVisitId` → öğe); `openReport` öğeyi bulur.
- "Sunulan gerçek aşama" serbest metin yerine **seçim**: planlanan yolculuğun aşamaları (yeni Web vekili `GET /CRM/VisitExecution/api/journeys/{id}/stages` → mevcut CRM ucu; izin: rapor yazma izniyle aynı okuma — mevcut PlannedVisits vekilinin izin desenini kopyala). Varsayılan = plandaki aşama.
- Ek seçenek "Plandaki aşama sunulmadı" → aşama alanları boş, `matchedPlan = false`.
- Gönderimde `actualContent`: `journeyId`, `stageId`, `stageIndex`, `stageCode`, `matchedPlan` (seçim plandakiyle aynıysa true) + görünen adlar. Serbest kod alanları formdan kalkar.
- Planlanan yolculuğu olmayan ziyarette bölüm gizlenir (bugünkü gibi boş gönderilir).
- **Yazan uç YOK** (SB-3c). Rapor belgesinde `ActualContent.JourneyId` dolu olması yeterli.

### 3. (E9-B4) Sonuç kodu
- Önce kök: kullanıcının gördüğü "İşlem başarısız" yanıtının gerçek şeklini bul (testle sabitle).
- Sunucu: `visit_report_outcome_code_required` (ve rapor doğrulamasının diğer kodları) her yolda `errors: [code, message]` ya da mevcut sözleşmedeki sırayla döner; ProblemDetails yolu da aynı zarfa çevrilir **ya da** Web vekili ProblemDetails'i zarfa dönüştürür (hangisi mevcut desene uyuyorsa; raporla).
- Web: istemci tarafı zorunlu alan kontrolü + kod → yerelleştirilmiş mesaj eşlemesi (rapor formundaki tüm `visit_report_*` kodları). Tanınmayan kod → sunucu metni.
- **Referans doğrulaması bu pakette YOK** (sonuç kodu kümesi yok; karar → yol haritası backlog). Ekrandaki "Sonuç kodları referans verilerinden gelir" notu kaldırılır ya da "serbest metin" olarak düzeltilir; boş datalist kaldırılır.

### 4. (E9-B5) İleri tarihli ziyarete sonuç / rapor yok
- `RecordVisitOutcome` `completed` ve `missed` için, `SubmitVisitReport` her durumda: `PlannedDate > bugün` → **409** `visit_not_yet_due`. "Bugün" = kiracı saat dilimi varsa o, yoksa UTC (hangisi mevcutsa; raporla). `rescheduled` serbest kalır.
- Taslak rapor kaydı (varsa) serbest kalır; yalnız gönderim / sonuç kapanır. Mobil aynı kurala tabi (sözleşme notuna girer).
- Web: ileri tarihli kartta "Tamamlandı" / "Rapor gönder" pasif + ipucu "Ziyaret günü gelmedi".
- Bu iki komut **mevcut** komut; yeni komut YOK (AUD-001 26 sabit).

### 5. (E9-B6) Onay, etiket, başlık
- "Tamamlandı" `showConfirm` ile onay ister.
- Kartta sonuç yerelleştirilmiş etiketle (completed / missed / rescheduled …; sözlükteki tüm `ExecutionOutcomes`).
- Sayfa başlığı menüyle aynı: **"Ziyaret Yürütme"** (7 dil; mevcut çeviriler menüdekiyle hizalanır).

### 6. (E7-B4) Düzenle → Kaydet yönlendirmesi
- Güncellemede yönlendirme her zaman mevcut `sessionId` ile. Oluşturmada dönen kimlik (bugünkü gibi).

### 7. (E8-B1) "Bu haftanın planı olarak kaydet"
- `showConfirm` ile onay ("N ziyaret planlanacak; plan kilitlenir").
- Başarıda toast (`ScheduledCount` ile) — bugün neden görünmediğini bul ve düzelt.
- Başarıdan sonra sayfa salt okunur duruma geçer: en basit yol **sayfayı yeniden yüklemek** (sunucu `readOnly`'yi hesaplıyor); toast yeniden yüklemeden sonra da görünmeli (ör. `sessionStorage` bayrağı ya da sorgu parametresi — mevcut desen varsa onu kullan).

## KORU / YAPMA
- **Yeni yazma komutu YOK** (AUD-001 26 sabit). Yalnız iki mevcut komutta doğrulama kuralı + bir okuma ucuna ek alan.
- `JourneyProgress` yazma YOK (SB-3c). Rapor belgesi şeması değişmez (yalnız mevcut alanlar dolar).
- Mobil sözleşmesi yalnız **ek** alan (`plannedContent`) + yeni hata kodu `visit_not_yet_due`; mevcut alan adları değişmez.
- Ziyaret Planlama ekran tasarımı (Rota, liste, yeni plan paneli) değişmez.
- Yeni metinler **7 dil** (en, tr, fr, es, zh, ar, ru); TR diakritikli. Görünüm resx'i; `SharedResource`'a dokunma.
- Seed / grant / göç YOK. Test kayıtları (`E2E-TUT-`) silinmez.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (699/0), CRM Application (2303/0/5; PII flake bilinen), mimari (38/1; **26 sabit**). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Takvim öğesi `plannedContent`'i planlanan ziyaretin `ContentItems`'ından doldurur (ürün kodu, aşama adı, adım başlıkları); `ContentItems` boşsa boş liste; eski alanlar değişmez.
2. Rapor gönderimi `actualContent.journeyId / stageId` taşıdığında rapor belgesinde saklanır (mevcut mapper ile).
3. Web rapor gövdesi: plandaki aşama seçiliyse `journeyId`, `stageId`, `stageIndex`, `matchedPlan=true`; "sunulmadı" → aşama boş, `matchedPlan=false` (JS / kaynak testi).
4. Sonuç kodu boş → yanıt `errors` dizisinde `visit_report_outcome_code_required`; Web eşlemesi TR mesaj verir.
5. İleri tarihli ziyarette `completed` sonuç ve rapor gönderimi → 409 `visit_not_yet_due`; bugün / geçmiş → geçer; `rescheduled` ileri tarihte geçer.
6. `form.js` güncellemede `sessionId`'ye yönlenir (kaynak testi).
7. Uygula: onay istenir; başarıda salt okunur duruma geçilir (kaynak testi ya da Web testi).
8. Yeni resx anahtarları 7 dilde var ve TR değeri anahtarın kendisi değil.

**Sabotaj (kırmızı kanıtla, geri al):**
1. Tarih kuralını kaldır → test 5 kırmızı.
2. `plannedContent` eşlemesini boş bırak → test 1 kırmızı.

### E4 (CT, fleet; kullanıcı girişi Beste; ayrı sekme)
- Ziyaret Yürütme 19 Eki: kartta `TUTUKON · Farkındalık`; rapor formunda planlanan adımlar.
- 9 Kas (ileri tarih) kartında "Tamamlandı" pasif.
- Bir **geçmiş tarihli** test ziyaretinde aşama seçimi plandan gelir; gönderilen raporda `journeyId` dolu (Mongo okuması). Bu bir kayıt işlemidir: test ziyaretinde, kullanıcı onayıyla.
- Sonuç kodu boş → TR mesaj.
- Başlık "Ziyaret Yürütme".
- Ziyaret Planlama: Düzenle → Kaydet → doğru ayrıntı sayfası; Uygula onay + mesaj + kilit (yeni bir test taslağında, kullanıcı onayıyla).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-E2E-FIX-1 · Ziyaret Yürütme raporu ("ne sunacağım" + plandan yolculuk/aşama) + plan uygula/düzenle
Repository: C:\tmp\e2e-fix-1 (worktree) · Branch: wp/e2e-fix-1 · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-E2E-FIX-1-visit-execution-report-and-plan-apply.md — önce tamamını oku (Bağlam tablosundaki dosya:satır kanıtları CT okumasıdır, doğrula). Ayrıca: …/E2E-TUTUKON-content-to-visit-plan.md (§E7–E9) · …/WP-SB-3b-journey-progress-resolver-v2.md (KORU: yazan uç SB-3c) · services/Diten.CrmService/src/**/Features/VisitReport/** · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitExecution/visit-execution.js · frontend/Diten.Web/Views/CRM/VisitExecution/Index.cshtml · frontend/Diten.Web/Controllers/CRM/CrmVisitExecutionController.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/{form,details}.js · .antigravity/rules/audit-trail-standard.md.

NE:
(1) E9-B2 — CRM takvim öğesine EK alan plannedContent (ContentItems özeti: ürün, rol, yolculuk, aşama adı/index, adım başlıkları); Web kart "#0" yerine ürün çipi + aşama adı; rapor formunda planlanan içerik listesi.
(2) E9-B1 ön koşul + E9-B3 — Web takvim öğelerini saklar; gerçek aşama = SEÇİM (yeni Web vekili journeys/{id}/stages → mevcut CRM ucu), varsayılan plandaki aşama, "sunulmadı" seçeneği; gönderimde journeyId/stageId/stageIndex/stageCode/matchedPlan. JourneyProgress YAZMA YOK (SB-3c).
(3) E9-B4 — "İşlem başarısız"ın gerçek yanıt şeklini bul; rapor doğrulama kodları zarfta; Web istemci zorunlu kontrolü + kod→TR mesaj eşlemesi; boş datalist / yanlış "referans verisi" notu düzeltilir; referans doğrulaması YOK.
(4) E9-B5 — RecordVisitOutcome (completed, missed) + SubmitVisitReport: PlannedDate > bugün → 409 visit_not_yet_due; rescheduled serbest; Web ileri tarihli kartta düğmeler pasif.
(5) E9-B6 — Tamamlandı showConfirm; kartta yerelleştirilmiş sonuç etiketi; sayfa başlığı "Ziyaret Yürütme".
(6) E7-B4 — form.js güncellemede sessionId'ye yönlen.
(7) E8-B1 — Uygula showConfirm + toast (neden çıkmadığını bul) + başarıdan sonra salt okunur (yeniden yükleme + toast korunur).
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (AUD-001 26 sabit); mobil sözleşmesi yalnız ek alan + yeni hata kodu; Rota/liste/yeni plan paneli tasarımı değişmez; yeni metinler 7 dil (TR diakritik), SharedResource'a dokunma; seed/grant/göç YOK; E2E-TUT kayıtları silinmez.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web (699/0) · CRM Application (2303/0/5, PII flake) · mimari (38/1, 26 SABİT); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–8. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "fix(crm,web): WP-E2E-FIX-1 — visit execution shows planned content, report carries planned journey/stage, not-yet-due guard, apply/edit UX" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), E9-B4 kök yanıt şekli, E8-B1 toast kökü, "bugün"ün saat dilimi kaynağı, mobil için yeni alan/kod listesi. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-07)
**Commit:** `4e3150f1a` (ajan `87555b958`, test dalının iki belge commit'i üzerine rebase, ff). Push: test dalı.

**CT K13:**
| Paket | Taban | Sonuç |
|---|---|---|
| CRM Application | 2303/0/5 | **2313 + 1 PII flake (bilinen) / 5** (+11 yeni) |
| Web | 699/0 | **711/0** (+12 yeni) |
| Mimari | 38/1 (26) | **38/1 (26 sabit)** |

**Kod okuması:**
- `plannedContent` planlanan ziyaretin `ContentItems`'ından, ek sorgu yok; eski alanlar aynı.
- Tarih kuralı `VisitReportValidation.ValidateDue` tek yerde; `RecordVisitOutcome` (rescheduled hariç) ve `SubmitVisitReport` kullanıyor; 409 `visit_not_yet_due`. `TimeProvider` isteğe bağlı parametre (DI'da kayıtlı değilse sistem saati).
- E9-B4 kökü: başarısız `Response<Guid>`'de `data` = boş GUID metni → istemci `body.data || body` ile `errors`'u kaçırıyordu. Hata artık kökteki `errors`'tan; 20 kod TR eşlemeli; vekil ProblemDetails'i zarfa çeviriyor.
- Aşama seçimi yeni vekil `api/journeys/{id}/stages` (takvimle aynı okuma izni); `journeyId / stageId` rapora gidiyor; `JourneyProgress` yazma yok.
- E7-B4: düzenlemede `sessionId`. E8-B1: onay + yeniden yükleme + `sessionStorage` toast.

**CT sabotajı (ajanınkinden ayrı):** tarih kuralı `>` → `>=` (bugün de yasak) + takvimde `StageName` boş → **9 test kırmızı** (`Today_and_past_visits_take_an_outcome_and_a_report(0)`, `Calendar_item_carries_…` ve mevcut rapor testleri). Geri alındı.

**Kabul edilen sınırlar (takip):**
- "Bugün" **UTC takvim günü** (CRM'de kiracı saat dilimi yok). TR'de 00:00–03:00 arası o günün ziyareti "günü gelmedi" sayılır; saha pratiğinde etkisiz. Kiracı saat dilimi kaynağı geldiğinde ona bağlanır (takip).
- Çok ürünlü ziyarette aşama seçici yalnız ilk ürünün yolculuğu için (rapor belgesinde tek `ActualContent`). Ürün başına gerçekleşen → **SB-3c** (`ContentActuals[]`).
- E8-B1 toast'ın eski kökü kodda bulunamadı (5 sn'de kayboluyordu, kalıcı değişiklik yoktu); yeni akışta yeniden yükleme sonrası gösteriliyor → E4'te bakılır.

**E4 (CT, bekliyor; fleet yeniden başlatma — CRM + Web değişti):** 19 Eki kartı `TUTUKON · Farkındalık` + rapor formunda adımlar · 9 Kas kartında düğmeler pasif · geçmiş tarihli test ziyaretinde aşama seçimi + Mongo'da `journeyId` (kayıt, onayla) · boş sonuç kodunda TR mesaj · başlık "Ziyaret Yürütme" · Düzenle → Kaydet doğru sayfa · Uygula onay + toast + kilit (yeni test taslağında, onayla).
