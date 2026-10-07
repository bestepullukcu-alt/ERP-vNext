# WORK PACKAGE — WP-VP-3B · Gün dengeleme + günlük / haftalık kapasite + yarım gün + taşma + izin engelli doktor

> **CT (SoR), 2026-10-07.**
> - **Tasarım:** [DESIGN-VP-FAZ3](DESIGN-VP-FAZ3-planning-engine.md) §2.4 · [yol haritası](ROADMAP-visit-planning.md) Faz 3 (B-5, B-7, C1–C5, MK-6/8/9).
> - **Kullanıcı:** "Faz 3 … paketle" (2026-10-07).
> - **Kapsam:** CRM motoru (gün ataması, kapasite, taşma), CRM çalışma takvimi denetleyicisi (yarım gün), önizleme DTO'su. Web yalnız zorunlu uyum.
> - **Ön koşul:** **WP-VP-3A birleşmiş olmalı.** Worktree 3A kabulünden sonra test dalından açılır.
>
> **Çalışma yeri:** worktree `C:\tmp\vp-3b`, dal `wp/vp-3b`, taban `test/crm-content-visit-e2e` (3A sonrası). Commit bu dala, push YOK.

## Bağlam (CT kod okuması, 2026-10-07; 3A sonrası satırlar kayabilir)
- **Gün seçimi** — motor hafta başına bir `Optimize` çağrısı yapıyor (`Application/Features/VisitPlanning/VisitPlanningEngine.cs:355–411`).
  - `TimeWindowInsertionEngine.Schedule` (`Application/Features/RouteOptimization/TimeWindowInsertionEngine.cs:82–144`) günleri sırayla 18:00'e kadar dolduruyor. **Günlük sınır yok, dengeleme yok** → "hepsi Pazartesi".
  - `ScheduleInOrder` :163–253 de aynı davranıyor.
- **Çalışma saati:** `RepWorkingHours(null, startLocation)` :379 → varsayılan 09:00–18:00, öğle 13–14 (`IRouteOptimizationDefaultsProvider.cs:32–35`).
  - `CycleCapacity.DailyWorkMinutes` kullanılmıyor; ziyaretler arası tampon `BetweenVisitTimeMinutes` :247.
- **Takvim:** `PlanningWorkingCalendar.cs:32–87`.
  - Gün başına `IWorkingDayChecker`; hata olursa Cmt / Paz ile `unresolved`.
  - **Yarım gün modellenmemiş** (:15).
  - CRM denetleyici yükü yalnız `Resolution, WorkingDayCount, IsWorkingDay, SelectionReason, ReasonCodes` taşıyor (`Infrastructure/CycleCapacity/WorkingCalendarWorkingDayCounter.cs:371–376`).
  - Platform yanıtında `Holiday.IsHalfDay` var (`services/Diten.Platform/**/WorkingCalendar/Provider/WorkingCalendarContracts.cs:71,82`), neden kodu `half_day_treated_as_working` (:43).
- **Kapasite:** `Domain/Entities/CycleCapacity.cs`.
  - `DailyWorkMinutes` :53, `DailyFixedMinutes` :172, `VisitMinutes` :182, `TypicalVisitMinutes` :193, `BetweenVisitTimeMinutes` :84.
  - Aylar (`Fte`, izin / toplantı / eğitim günleri) :296–343.
  - Motor arz olarak dönem toplamını kullanıyor (:426–465). Haftalık / günlük yok (C5).
- **İzin:** izin engelli doktor yine planlanıyor (bayrak :304, süzme yok).
- **Önizleme:** `VisitPlanningModels.cs` — `Unscheduled` nedenleri `period_exhausted, no_feasible_availability_window, duration_exceeds_working_day, missing_location, invalid_input` (`RouteOptimizationModels.cs:113–129`); `SupplyDemandSummary` :109–115.

## NE
### 1. Günlük bütçe (MK-8) ve yarım gün (MK-9)
- **Günlük ziyaret bütçesi (dk)** = `DailyWorkMinutes − DailyFixedMinutes()` (kapasite yoksa bugünkü varsayılan saatlerden türet; kuralı belgele).
  - Yarım günde bütçenin yarısı; tatil / hafta sonunda 0.
  - Bütçe ziyaret süresi + ziyaretler arası tampon toplamını sınırlar.
- **Günlük üst sınır (sayı)** = bütçe ÷ (tipik ziyaret süresi + tampon), aşağı yuvarla. Önizlemede bilgi olarak gösterilir (mockup "günde en çok N"; metin veriden).
- **Yarım gün:** CRM çalışma takvimi denetleyicisi Platform yanıtındaki yarım günü taşır.
  - `IsHalfDay` / ilgili neden kodu → `PlanningWorkingCalendar` gün başına `{ date, kind: working | half | holiday | weekend }` üretir.
  - Önizlemede `halfDayDates` (+ `nonWorkingDates` aynen).
  - Platform'a dokunma; yalnız CRM tarafında yanıttan oku. Sözleşmede alan yoksa raporla ve neden koduyla çöz.
- Gün başlangıcı 09:00 (varsayılan sağlayıcı). Bitiş = başlangıç + günlük çalışma dakikası (öğle arası bugünkü gibi).

### 2. Gün dengeleme (C1)
- Haftanın ziyaretleri önce **kurum grubu** (aynı hesap + o hesabın eczaneleri) halinde toplanır.
- Gruplar büyükten küçüğe, **o anki yükü en düşük çalışma gününe** atanır (bütçe doluysa sonraki en boş gün). Aynı kurumun doktorları aynı gün kalır.
  - Grup tek güne sığmıyorsa bölünür; bölme kuralını belgele.
- Gün içi sıra ve saat: mevcut rota iyileştirici yalnız **o günün** ziyaretleriyle çağrılır.
- Elle sıra (`ManualVisitOrder`, 3A'da hafta başına) varsa gün içi sırayı belirler, günü değiştirmez.
- **Hafta sonu ve tatile ziyaret düşmez** (VP-FIX-1 kuralı korunur).

### 3. Taşma (MK-6) ve haftalık kapasite (B-7, C5)
- Haftanın bütçesine sığmayan ziyaret, sıklık aralığını bozmadan **sonraki taslak haftaya** kayar.
  - Önizlemede `shifted[]`: `{ targetType, targetId, contactId, displayName, fromWeek, toWeek, reason: capacity_full | holiday | half_day }`.
  - Dönem sonunu aşarsa `unscheduled: period_exhausted`.
  - Onaylı haftalara (3A) taşma **yapılmaz**; onaylı haftanın kapasitesi dolu sayılır.
- Önizleme hafta başına `weekCapacity[]`: `{ weekStart, workingDays, halfDays, holidays, capacityMinutes, plannedMinutes, visitCount, dailyCap }`.
- Dönem özeti `{ capacityMinutes, plannedMinutes }`: arz ve talep **aynı birimle** (C5). Bugünkü `SupplyDemandSummary` geriye uyum için kalır, yeni alanlar ek.
- Süre: içerik çözücüsünün süresi, yoksa tipik ziyaret modeli (C4; bugünkü `DefaultDuration`).

### 4. İzin engelli doktor
- İzin durumu `blocked` olan doktor planlanmaz: `unscheduled: consent_blocked`. `unknown` planlanır, uyarı taşır.
- Kampanya hedeflemedeki "blocked ⇒ excluded" kuralıyla aynı kaynak.

### 4b. Takvim sorgu sayısı (3A §37 takibi)
- 3A'dan sonra çalışma takvimi tüm dönem için **gün gün** soruluyor: önizleme başına ~90 istek (eskiden ≤ 42).
- Yarım gün desteğini eklerken aralığı **tek istekle** (ya da hafta başına bir) okuyan yolu kullan. Platform'da aralık ucu varsa onu, yoksa istek başına önbelleği kullan.
- Önce / sonra istek sayısını raporla. Platform'a dokunma.

### 5. Web zorunlu uyum
- Önizleme yanıtının yeni alanları bugünkü ekranı bozmamalı.
- Haftalar / Rota sekmeleri bugünkü gibi çalışır; gün dağılımı değişeceği için Rota günleri farklı olacak.
- Yeni alanların gösterimi Faz 4'te.

## KORU / YAPMA
- **Yeni yazma komutu YOK.** Mimari test listesiz sayısı 3A'dan sonraki değerde (27) kalır.
- 3A'nın hafta modeli, onay / yeniden aç kuralları ve sıklık dağılımı **değişmez**. Taşma dağılımı bozmaz, yalnız kaydırır.
- Ürün listesi (3C) ve durum okumaları (3D) bu pakette yok.
- Platform'a, Rota tasarımına ve mobil sözleşmeye dokunma. Mobil yalnız ek alan.
- Seed / grant / göç / indeks YOK.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM / Web / mimari: 3A kabulündeki değerler (taban o an ölçülür). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Bütçe: `DailyWorkMinutes` 480, sabit 60, ziyaret 20 + tampon 10 → günlük sınır 14; yarım günde 7; tatilde 0.
2. Dengeleme: 5 kurumda 40 ziyaret, 5 çalışma günü → günlük yük farkı ≤ en büyük grup; aynı kurum aynı gün; Cmt / Paz / tatile 0.
3. Yarım gün: takvim yanıtında yarım gün → o gün bütçe yarı; önizlemede `halfDayDates`.
4. Taşma: haftalık kapasite aşılınca fazlası sonraki taslak haftaya, `shifted` nedenli; son haftada `period_exhausted`; onaylı haftaya taşma yok.
5. `weekCapacity` hafta başına doğru; dönem özeti dakika birimiyle.
6. İzin `blocked` → `consent_blocked`, planlanmaz; `unknown` planlanır.
7. Elle sıra gün içi sırayı belirler, günü değiştirmez.

**Sabotaj (kırmızı kanıtla, geri al):**
1. Günlük bütçe sınırını kaldır → test 2 kırmızı ("hepsi Pazartesi" geri gelir).
2. Yarım gün çarpanını 1 yap → test 3 kırmızı.

### E4 (CT, fleet; salt okuma önizleme)
- Q4 planı önizlemesi: ziyaretler hafta günlerine yayılmış.
- 29 Ekim (tatil) boş, 28 Ekim (yarım gün) yarı dolu.
- `weekCapacity` ve `shifted` dolu.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (3A kabulünden SONRA)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-3B · Gün dengeleme + günlük/haftalık kapasite + yarım gün + taşma + izin engelli doktor
Repository: C:\tmp\vp-3b (worktree) · Branch: wp/vp-3b · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-3B-day-balancing-capacity.md — önce tamamını oku (Bağlam dosya:satır CT okumasıdır, 3A sonrası kayabilir; doğrula). Tasarım: …/DESIGN-VP-FAZ3-planning-engine.md (§2.4). 3A §37'sini de oku (hafta modeli). Ayrıca: services/Diten.CrmService/src/**/Features/{VisitPlanning,RouteOptimization,CycleCapacity}/** · **/Domain/Entities/CycleCapacity.cs · **/Infrastructure/CycleCapacity/WorkingCalendarWorkingDayCounter.cs · services/Diten.Platform/**/WorkingCalendar/Provider/WorkingCalendarContracts.cs (YALNIZ OKU).
NE:
(1) Günlük bütçe = DailyWorkMinutes − DailyFixedMinutes (yoksa varsayılan saatlerden), yarım gün yarı, tatil/hafta sonu 0; günlük üst sınır sayısı önizlemede; CRM takvim denetleyicisi Platform yanıtındaki yarım günü taşır (Platform'a dokunma), PlanningWorkingCalendar gün türü working|half|holiday|weekend, önizlemede halfDayDates.
(2) Gün dengeleme: kurum grupları büyükten küçüğe en boş çalışma gününe, aynı kurum aynı gün (bölme kuralı belgeli), gün içi sıra rota iyileştirici yalnız o günle; ManualVisitOrder gün içi sırayı belirler; hafta sonu/tatile ziyaret yok.
(3) Taşma: sığmayan sonraki taslak haftaya (sıklık aralığı korunur), önizlemede shifted[] {targetType,targetId,contactId,displayName,fromWeek,toWeek,reason capacity_full|holiday|half_day}; dönem sonu → period_exhausted; onaylı haftaya taşma yok. weekCapacity[] {weekStart,workingDays,halfDays,holidays,capacityMinutes,plannedMinutes,visitCount,dailyCap} + dönem özeti dakika birimi (SupplyDemandSummary geriye uyum).
(4) İzin blocked → unscheduled consent_blocked; unknown planlanır + uyarı. (4b) Takvim aralığı tek istekle / önbellekle (3A sonrası ~90 istek), önce/sonra say.
(5) Web zorunlu uyum: yeni alanlar ekranı bozmaz.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz sayı 3A sonrası değerde); 3A hafta/onay/sıklık kuralları değişmez; ürün listesi (3C) ve okumalar (3D) yok; Platform/Rota tasarımı/mobil sözleşme değişmez (yalnız ek alan); seed/grant/göç/indeks YOK.
DOĞRULA (E2): tabanı 3A sonrası ölç, yalnız farkı raporla — CRM Application · Web · mimari; build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–7. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(crm): WP-VP-3B — day balancing with daily budget from cycle capacity, half days, overflow to next week, weekly capacity, consent-blocked excluded" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), bütçe/sınır formülü, dengeleme ve bölme kuralı, yarım gün kaynağı (Platform alanı/neden kodu), mobil için yeni alanlar. §22 TÜRKÇE. K13.
```
