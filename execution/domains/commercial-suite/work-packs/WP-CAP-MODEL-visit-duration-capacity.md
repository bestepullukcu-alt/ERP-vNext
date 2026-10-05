# WORK PACKAGE — WP-CAP-MODEL · Dönem kapasitesi: tek ziyaret süresi modeli (tipik ziyaret + ziyaret başına rapor), yazar FTE, mikro-hedefleme kırpma, dönem kodu önerisi (CRM)

> **CT (SoR), 2026-10-05.**
> - **Kaynak:** `mockups/cycle-planning/BRIEF-cycle-periods-capacity.md` §2.4 + `mockups/cycle-planning/CYCLE-PLANNING-mockup-analysis.md` §2 (K-1, K-2, K-5, E8) + §5 (kullanıcı: "kabul CAP-MODEL'i paketle").
> - **Sonraki:** CYC-UI (Web; iki sayfa mockup'a göre) bu paketin sözleşmesini kullanır.
> - **Kapsam:** yalnız CrmService (+ testleri). Web DOKUNMA.
>
> **Çalışma yeri:** worktree `C:\tmp\cap-model`, dal `wp/cap-model`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Sorun (CT kod okuması)
Aynı alanlar iki yerde farklı anlamda:
- **Kapasite:** `CycleCapacity.MinutesPerVisit()` = `PromoProductTime + NonPromoProductTime` (`Domain/Entities/CycleCapacity.cs:130`); rapor **gün başına**: `DailySpendMinutes()` = `TravelingTime + ReportDuration + QuizDuration` (:127); hesap `Application/Features/CycleCapacity/Rules/CycleCapacityCalculator.cs:101-102`.
- **Planlama:** `ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount)` = sayı × ürün süresi + `ReportDuration` (**ziyaret başına**) (`Rules/ActivityTimeBudgetCalculator.cs:42-53`); kullananlar `VisitContentSequenceResolver.cs:318` (ürün sayılarıyla), `VisitPlanningEngine.cs:653` `DefaultDuration` (`VisitDuration(capacity, 0, 0)` → yalnız rapor süresi).
- Sonuç: arz (kapasite) ile talep (planlama) farklı süreyle hesaplanıyor.
- **Diğer:** mikro-hedefleme dakikası saha gününe kırpılmıyor (`CycleCapacityMonth.MicroTargetingMinutes()` :261); FTE yalnız sunucu varsayılanı (`EnsureMonthlyFte` :153, `FteSource` `interim-default`; `authored` ayrılmış ama yazılmıyor :269-275); dönem kodu tamamen elle (`CreateCyclePeriodHandler`, küçük harfe çevrilir, tekil, değişmez).

## NE
1. **Tek ziyaret süresi modeli (K-1):**
   - Yeni alanlar (nullable): `TypicalPromoCount`, `TypicalNonPromoCount`, `ReportMinutesPerVisit`.
   - **Yeni model** (üçü dolu): `TypicalVisitMinutes = TypicalPromoCount × PromoProductTime + TypicalNonPromoCount × NonPromoProductTime + ReportMinutesPerVisit`; günlük sabit = `TravelingTime + QuizDuration` (rapor gün başına **düşülmez**); `ReportDuration` 0 yazılır.
   - **Eski kayıt** (yeni alanlar boş): bugünkü hesap aynen (okuma anında; **veri yazılmaz**, göç yok); DTO'da `visitModel: legacy`. Kayıt yeni modelle güncellenince `typical`.
   - **Tek formül, tek yer:** domain metotları `TypicalVisitMinutes()`, `DailyFixedMinutes()`, `ReportMinutesForVisit()`, `VisitMinutes(promoCount, nonPromoCount)`; `ActivityTimeBudgetCalculator.VisitDuration` ve `CycleCapacityCalculator` bunları kullanır. Eski kayıtta `VisitMinutes(p, n)` bugünkü sonucu verir (SB-3b davranışı değişmez).
   - `VisitPlanningEngine.DefaultDuration` → yeni modelde `TypicalVisitMinutes()`; eskide bugünkü değer.
   - **Doğrulama:** tipik sayılar 0..ilgili sınır (`MaxPromoProducts / MaxNonPromoProducts`), ikisi birden 0 olamaz (400 `typical_visit_empty`), sınırı aşarsa 400 `typical_count_exceeds_max`; rapor 0..mevcut etkinlik dakika sınırı; `TypicalVisitMinutes` > 0 ve ≤ mevcut ziyaret üst sınırı (480); günlük sabit < iş günü (mevcut kural, yeni günlük sabitle).
   - Create / Update / Preview komutları ve DTO'lar yeni alanları taşır; Update'te üçü birlikte gelir ya da hiçbiri (karışık → 400 `typical_visit_incomplete`).
2. **Yazar FTE (K-5):** aylık satırda `Fte` gönderilirse `FteSource = authored` (mevcut aralık kuralı, 0 dahil — boş pozisyon); gönderilmezse kayıttaki değer / kurum içi varsayılan (bugünkü davranış). Okumada kaynak gösterilir.
3. **Mikro-hedefleme kırpma (E8):** ay hesabında `min(MicroTargetingDayCount, FieldDays) × MicroTargetingDuration`.
4. **Hesap DTO'su (ekranın şelalesi):** ay satırına `DailyFixedMinutes`, `RemainingMinutes` (ziyarete kalan), `TypicalVisitMinutes`; hesap köküne `VisitModel`, `TypicalVisitMinutes`, `DailyFixedMinutes` ve **toplamlar** nesnesi (`WorkingDays, DeductedDays, FieldDays, AvailableMinutes, DailyFixedMinutes, MicroTargetingMinutes, RemainingMinutes, Visits, AverageFte`). Yuvarlama bugünkü gibi (ay başına, AwayFromZero, toplam = aylar toplamı). Takvim çözülemezse bugünkü "değer yok" kuralı (K-4) aynen.
5. **Dönem kodu önerisi (K-2):** `GET /api/crm/cycle-periods/code-suggestion?scopeType=&countryScope=&legalEntityId=&businessUnitId=&year=` → `{suggestedCode, nextSequenceInYear}`.
   - Biçim: ülke `{ISO2}-{YYYY}-{NN}`, tüm şirket `GM-{YYYY}-{NN}`, tüzel kişi / iş birimi kendi kısa kodu (katalogdan; yoksa `LE` / `BU`) + `-{YYYY}-{NN}`; `NN` = kapsam + yıl içindeki ilk boş sıra (kapalılar dahil, mevcut `SequenceTaken` kuralıyla).
   - Yalnız **öneri**: oluşturma kodu yine istekten gelir; tekillik, küçük harf saklama ve değişmezlik kuralları aynen.

## KORU / YAPMA
- Dönem kuralları (aktifte tarih / yıl / sıra / kapsam değişmez, kapsam her zaman değişmez, aktif çakışma yasağı, çözüm önceliği) DEĞİŞMEZ.
- Takvim çözülemezse sayı yok (K-4) — DEĞİŞMEZ. Takvim ülkesi kuralı (K-8) DEĞİŞMEZ.
- Planlama / içerik çözücü davranışı **eski kayıtlarda** değişmez; yeni modelli kayıtta süreler yeni formülden.
- Veri göçü YOK; eski belge okunur (class-map sıkı — yeni alanlar class-map'e).
- Web DOKUNMA (CYC-UI ayrı). Merkezi log YOK.
- **DUR:** mevcut bir tüketici (planlama motoru, mobil sözleşme, rapor) `MinutesPerVisit` / `DailySpendMinutes`'ı dışarı açıyor ve anlamı değişince kırılıyorsa → raporla.

## Acceptance
- **E2:** CRM 0 kırmızı (taban ölç; son CT 2187/0/5 — KP-5b henüz birleşmedi), Web 0 kırmızı (taban 473; CRM sözleşme adları değişirse), build 0 hata.
  - **Yeni testler:** yeni modelde tipik süre + günlük sabit (rapor gün başı yok) + ay hesabı (bilinen örnek: 2×12 + 1×5 + 5 = 34 dk); eski kayıt bugünkü sayıyı aynen verir (golden örnek testleri yeşil); `VisitDuration` yeni / eski modelde; planlama `DefaultDuration` yeni modelde tipik süre; doğrulama kodları (`typical_visit_empty`, `typical_count_exceeds_max`, `typical_visit_incomplete`); yazar FTE (`authored`, 0 kabul, gönderilmezse korunur); mikro-hedefleme kırpma; toplamlar = aylar toplamı; takvim çözülemezse değer yok; kod önerisi (ülke / tüm şirket / tüzel kişi / iş birimi biçimi, ilk boş sıra, kapalı sıralar dolu sayılır); class-map round-trip (eski belge okunur).
  - **Sabotaj:** (1) yeni modelde raporu gün başına da düş (çift sayım) → tipik model testi kırmızı; (2) eski kayıt yolunu yeni formüle çevir → golden eski sonuç testi kırmızı.
- **E4 (CT, CYC-UI sonrası):** kapasite ekranında tipik ziyaret + canlı hesap; eski bir kapasite açılınca aynı sayı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-CAP-MODEL · Dönem kapasitesi: tek ziyaret süresi modeli (tipik ziyaret + ziyaret başına rapor), yazar FTE, mikro-hedefleme kırpma, dönem kodu önerisi (CRM)
Repository: C:\tmp\cap-model (worktree) · Branch: wp/cap-model · commit bu dala, push YOK · yalnız CrmService (+ testleri)

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-CAP-MODEL-visit-duration-capacity.md — önce tamamını oku (Sorun / NE / KORU / Acceptance). Ayrıca: …/mockups/cycle-planning/{BRIEF-cycle-periods-capacity.md (§2.4), CYCLE-PLANNING-mockup-analysis.md (§2, §5)} · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{CycleCapacity, CyclePeriod}.cs · Application/Features/CycleCapacity/** (Rules/CycleCapacityCalculator, Rules/ActivityTimeBudgetCalculator, Validation, Mapper, Models, Create/Update/Preview) · Application/Features/CyclePeriod/** (CreateCyclePeriodHandler, Validation, ScopeRules, catalogs) · Application/Features/VisitPlanning/VisitPlanningEngine.cs (:653) · Application/Features/VisitContentSequence/VisitContentSequenceResolver.cs (:318) · Persistence/DependencyInjection.cs (class-map) · memory crm-classmap-rejects-unknown-elements, mod0155-fu06-cycle-capacity-pack.

NE: (1) Nullable TypicalPromoCount, TypicalNonPromoCount, ReportMinutesPerVisit. Yeni model (üçü dolu): TypicalVisitMinutes = tP×PromoProductTime + tN×NonPromoProductTime + ReportMinutesPerVisit; günlük sabit = Travel + Quiz (rapor gün başı YOK), ReportDuration 0 yazılır. Eski kayıt (boş): bugünkü hesap aynen, okuma anında, veri yazılmaz, DTO visitModel legacy. Tek formül domain metotlarında (TypicalVisitMinutes, DailyFixedMinutes, ReportMinutesForVisit, VisitMinutes(p,n)); ActivityTimeBudgetCalculator.VisitDuration + CycleCapacityCalculator bunları kullanır; eski kayıtta VisitMinutes bugünkü sonuç; VisitPlanningEngine.DefaultDuration yeni modelde TypicalVisitMinutes. Doğrulama: tipik sayılar 0..Max*, ikisi 0 → 400 typical_visit_empty, sınır aşımı 400 typical_count_exceeds_max, Update'te üçü birlikte ya da hiçbiri (400 typical_visit_incomplete), süre >0 ve ≤480, günlük sabit < iş günü. Create/Update/Preview + DTO'lar. (2) Aylık Fte gönderilirse FteSource authored (0 dahil, mevcut aralık), gönderilmezse korunur/varsayılan. (3) Mikro-hedefleme min(gün, saha günü) × süre. (4) Hesap DTO: ay satırına DailyFixedMinutes, RemainingMinutes, TypicalVisitMinutes; köke VisitModel, TypicalVisitMinutes, DailyFixedMinutes, totals {WorkingDays, DeductedDays, FieldDays, AvailableMinutes, DailyFixedMinutes, MicroTargetingMinutes, RemainingMinutes, Visits, AverageFte}; yuvarlama ay başına AwayFromZero; takvim çözülemezse değer yok (değişmez). (5) GET /api/crm/cycle-periods/code-suggestion?scopeType&countryScope&legalEntityId&businessUnitId&year → {suggestedCode, nextSequenceInYear}; biçim ülke ISO2 / tüm şirket GM / LE-BU kısa kod (yoksa LE/BU) + -YYYY-NN; NN ilk boş sıra (kapalılar dahil); yalnız öneri, kod kuralları aynen.
KORU/YAPMA: dönem kuralları, takvim çözülemez=değer yok, takvim ülkesi kuralı DEĞİŞMEZ; eski kayıtta planlama/içerik davranışı aynı; veri göçü yok; yeni alanlar class-map'e; Web DOKUNMA; merkezi log yok.
DOĞRULA (E2): cd C:\tmp\cap-model; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban ölç; PiiMasking flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 473); build 0 hata. Yeni testler WP Acceptance. Sabotaj: (1) yeni modelde raporu gün başına da düş → kırmızı; (2) eski kayıt yolunu yeni formüle çevir → golden eski sonuç kırmızı; geri al. TestResults/*.trx izleniyor, klasörü silme. Commit ("feat(crm): WP-CAP-MODEL — single visit duration model (typical visit, report per visit), authored FTE, micro-targeting clip, cycle period code suggestion" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: MinutesPerVisit/DailySpendMinutes'ı dışarı açan bir tüketici anlam değişince kırılıyorsa → DUR + raporla.
```
