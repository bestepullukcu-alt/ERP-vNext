# WORK PACKAGE — WP-VP-3A · Dönem planı + hafta durumu (onayla / yeniden aç) + tek plan kuralı + sıklık dağılımı

> **CT (SoR), 2026-10-07.**
> - **Tasarım:** [DESIGN-VP-FAZ3](DESIGN-VP-FAZ3-planning-engine.md) §2.1–2.3 · [yol haritası](ROADMAP-visit-planning.md) Faz 3 (B-4, D3, B1–B3, MK-3/4/7, K-2).
> - **Kullanıcı:** "Faz 3 … paketle" (2026-10-07).
> - **Kapsam:** yalnız CRM (oturum, motor, uygula, yeni yeniden-aç komutu). Web'de yalnız zorunlu uyum (bugünkü ekran kırılmasın); yeni ekran Faz 4'te.
>
> **Çalışma yeri:** worktree `C:\tmp\vp-3a`, dal `wp/vp-3a`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel:** WP-VP-3D (okuma uçları) ayrı dalda. Çakışma beklenen tek yer `VisitPlanningModels.cs`; ek alanlar, rebase'de birleşir.

## Bağlam (CT kod okuması, 2026-10-07)
Kısaltmalar: **APP** = `services/Diten.CrmService/src/Diten.CrmService.Application/Features` · **DOM** = `…/Diten.CrmService.Domain` · **PER** = `…/Diten.CrmService.Persistence`.

- **`PlanningSession`** (`DOM/Entities/PlanningSession.cs`):
  - alanlar: `Status` :32, `Selection` :35–86, `GenerationState` :90, `CommittedPlannedVisitIds` :45, `ManualVisitOrder` :49, `TargetWeekStart` :52;
  - durumlar `draft / generated / committed / archived` :111–118, yalnız ileri geçiş (`CanTransition` :138–154).
  - **Tekillik yok:** indeksler tekil değil (`PER/DependencyInjection.cs:1866–1877`); `ListByPeriodAndResourceAsync` var ama hiçbir yerde çağrılmıyor (`PER/Repositories/PlanningSessionRepository.cs:36`).
- **Komutlar** (`APP/VisitPlanning/Handlers/CommandHandlers/PlanningSessionCommandHandlers.cs`):
  - Oluştur :12–84;
  - Güncelle :108–182 (`MergeSelection` :187; `RequestedStatus` yalnız ileri, `committed`'a gidemez :166);
  - Uygula :206–304 (tüm haftaların atomları + oturum `committed`; ikinci uygulama `planning_session_already_committed` :248);
  - Yeniden planla :315–381.
  - Atomik iş birimi: `PER/Repositories/PlanningSessionApplyUnitOfWork.cs` (`ApplyAsync` :31, `ReplanAsync` :128).
- **Motor** (`APP/VisitPlanning/VisitPlanningEngine.cs`):
  - haftalar: `ResolveWeeksStart` :698, `BuildWeeks` :712, `MaxWeeks = 6` :34;
  - uygula: `BuildApplyAsync` :114–131, `BuildAtomAsync` :467–549;
  - içerik / aşama: `ResolveContentAsync` :555, `PendingBefore` :572, `ProjectContentAsync` :591.
- **Sıklık** (`APP/VisitPlanning/FrequencyExtendPlanner.cs:25–80`):
  - `PeriodType` yok sayılıyor (`DOM/Entities/VisitFrequencyPolicy.cs:59–62`);
  - bilinmeyen sıklık ya da `RequiredVisitCount ≤ 1` → yalnız hafta 0;
  - motor `FrequencyStatus / RequiredVisitCount`'u atıyor (:342–349).
- **Eczane** :322–327 ve doktorsuz kurum :329–339: varsayılan süre, sıklık yok.
- **Uçlar:** `Api/Controllers/CRM/VisitPlanningController.cs` (preview :35, apply :46, re-plan :58, sessions :71/100/106/117). Web vekili `frontend/Diten.Web/Controllers/CRM/VisitPlanningController.cs:137–272`.
- **Denetim:** CRM izlerinin hepsi aday; yeni komut mimari testte listesiz sayılır (26 → 27). Faz 8'de bağlanacak (kullanıcı kararı AUD-001 = A, en son).

## NE
### 1. Tek etkin plan (MK-3, D3)
- Oluştur: aynı (kiracı, temsilci, dönem) için **arşivli olmayan** plan varsa 409 `planning_session_exists`; yanıtta mevcut plan kimliği. Kontrol depo okuması + mümkünse yarış koruması (tekil kısmi indeks önerisi raporla; **indeks ekleme**, mevcut çoklu veri var).
- **Mevcut çoklu planlar okunur** (göç yok); kural yalnız yeni oluşturmada.
- Boş taslak arşivi: Update `RequestedStatus = archived` yalnız **hedefi olmayan ve onaylı haftası olmayan** planda; aksi 409 `planning_session_not_empty`. (Bugün arşive zaten gidilebiliyor mu → doğrula, kuralı ekle.)
- Liste DTO'suna `isEmpty` (hedef sayısı 0) — Faz 4 "boş taslak" rozeti için.

### 2. Hafta modeli (MK-3)
- `PlanningSession.Weeks: List<PlanningWeek>` — yalnız **onaylı / yeniden açılmış** haftalar saklanır:
  - `WeekStart` (Pazartesi, `yyyy-MM-dd`), `Status` (`approved | reopened`), `ApprovedAt`, `ApprovedBy`, `PlannedVisitIds`, `ManualVisitOrder`;
  - `History[]`: `{ At, By, Action (approve | reopen), Reason }`.
- Class-map kaydı (string-Guid; **CRM yeni tip GUID tuzağı**, bkz. `RegisterClassMaps`). Eski belgeler (alan yok) sorunsuz okunmalı.
- **Hafta durumu türetme** (tek yerde, saf işlev): `past` (hafta sonu < bugün) · `approved` · `draft` · `empty`.
  - `draft` = onaysız ve bu haftaya ziyaret düşüyor; `empty` = düşmüyor.
  - "Bugün" = UTC takvim günü (WP-E2E-FIX-1 ile aynı kaynak).
- Oturum ayrıntı DTO'su ve önizleme **tüm dönem haftalarını** durumlarıyla döner: `weeks[{ weekStart, isoWeek, status, visitCount, approvedAt?, history? }]`.

### 3. Haftayı onayla = `apply` + `weekStart`
- `POST api/crm/visit-plan/apply` gövdesine isteğe bağlı `weekStart`.
- **`weekStart` varsa:**
  - motor dönemi üretir, **yalnız o haftanın** ziyaretlerini atom olarak yazar;
  - `Weeks`'e `approved` ekler (ya da `reopened` → `approved`) ve geçmişe `approve` kaydı düşer;
  - oturum `committed` **olmaz** (dönem planı açık kalır; bugünkü "uygulanmış" karşılığı en az bir onaylı hafta).
- Kurallar:
  - geçmiş hafta → 409 `week_in_past`;
  - zaten onaylı → 409 `week_already_approved`;
  - dönem dışı / Pazartesi değil → 400 `invalid_week`.
- **Onaylı haftalar dondurulur (S-1):** sonraki önizlemeler onaylı haftaların ziyaretlerini yeniden üretmez, "sabit" sayar (kapasite / sıklık hesabına girer).
- **`weekStart` yoksa:** bugünkü davranış aynen (tüm dönem, `committed`). Kod yorumunda "Faz 4'te kaldırılacak" işareti. Bugünkü testler yeşil kalır.
- Atomiklik: mevcut `PlanningSessionApplyUnitOfWork` deseni (işlem + telafi).

### 4. Haftayı yeniden aç — **yeni komut** `ReopenPlanningWeek`
- `POST api/crm/visit-plan/sessions/{id}/weeks/{weekStart}/reopen`, gövde `{ reason, expectedVersion }`.
- İzin: uygula ile aynı (apply + planned-visit manage); sahiplik `VisitOwnership` (yabancı plan 404).
- Kurallar:
  - gerekçe zorunlu, ≥ 10 karakter → 400 `reopen_reason_required`;
  - hafta onaylı değilse 409 `week_not_approved`;
  - geçmiş hafta → 409 `week_in_past`.
- Etki:
  - haftanın **sonucu / raporu olmayan** planlı ziyaretleri `cancelled`, iptal nedeni `week_reopened` (mevcut iptal alanı / deseni; yoksa raporla);
  - **sonucu / raporu olanlar kalır** ve yeniden onayda sabit sayılır;
  - hafta `reopened`; geçmişe `reopen + reason` yazılır.
- Atomik: iptaller + oturum tek iş birimi.
- **Denetim:** komut mimari testte listesiz kalır (26 → **27**). Faz 8'de bağlanır. `audit-ledger`'a **elle satır ekleme** (CI reddeder); raporda belirt.

### 5. Ufuk ve sıklık dağılımı (B1, B2, MK-7, K-2)
- `MaxWeeks = 6` kalkar: ufuk **dönem sonu**.
- Başlangıç = `max(dönem başı, bugünün haftası)`. Geçmiş haftalar üretilmez.
  - `TargetWeekStart` artık yalnız "ekranda açılacak hafta" bilgisidir; üretimi kısıtlamaz. Bugünkü kullanımını raporla.
- Dönemde gereken ziyaret = politika sayısı × dönemdeki `PeriodType` birimi:
  - `week` → dönemdeki çalışma haftası (en az bir çalışma günü olan) sayısı;
  - `month` → dönemle kesişen ay sayısı (kısmi ayı oranla değil tam say; kuralı belgele);
  - `period` / `cycle` → 1.
  - Bilinmeyen `PeriodType` → dönem.
- **Bilinmeyen sıklık** → dönemde 1 + `frequencyStatus = unknown`.
- **Eczane:** hesap için sıklık politikası (hedef türü hesap / eczane) varsa o, yoksa dönemde 1 + `unknown`.
- **Kalan** = gereken − (yapılan + onaylı haftalardaki planlı, iptal hariç). "Yapılan" = sonucu `completed` olan planlı ziyaret (rapor bağlantısıyla).
- Kalan, **kalan taslak haftalara eşit aralıkla** dağıtılır (aralık = kalan hafta / kalan ziyaret, yuvarlama kuralı belgeli). Aynı haftaya aynı doktordan iki ziyaret yalnız kalan > kalan hafta ise.
- `FrequencyExtendPlanner` bu kurala çevrilir; motor `frequencyStatus` ve `requiredVisitCount`'u **atmaz**, slot ve aday modeline taşır (3D gösterecek; alanı burada ekle).

### 6. Web zorunlu uyum
- Bugünkü "Bu haftanın planı olarak kaydet" düğmesi **`weekStart` = ekranda seçili hafta** ile çağrılır. Böylece bugünkü ekran yeni modeli kullanır.
- Başarı mesajı ve kilit davranışı yeni modele uyar:
  - kilit yalnız onaylı haftada (salt okunur bant);
  - diğer haftalar düzenlenebilir;
  - "Yeniden planla" bugünkü gibi yalnız eski `committed` planlarda.
- Yeniden açma düğmesi Faz 4'te (bu pakette Web'e ekleme). Yeni metin varsa 7 dil.
- Liste: arşivli planlar listeden çıkar (mevcutsa), boş plan arşivi Faz 4'te.

## KORU / YAPMA
- Yeni komut **yalnız** `ReopenPlanningWeek`. Diğer her şey mevcut komutlarla (oluştur / güncelle / uygula). Mimari testte listesiz komut **27** olmalı, fazlası değil.
- Göç YOK; eski planlar (`committed`, `generated`) okunur ve çalışır. Seed / grant / indeks YOK.
- Gün dengeleme, kapasite, yarım gün, izin süzme **3B'de**; ürün listesi **3C'de**; durum okumaları **3D'de**. Bu pakette dokunma.
- Rota sekmesi tasarımı ve mobil sözleşme değişmez. Mobil yalnız **ek** alan (`weeks`, `frequencyStatus`).
- `E2E-TUT-` ve canlı test kayıtları silinmez.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM Application (2347/0/5; PII flake bilinen), Web (733/0), mimari (38/1; listesiz **26 → 27**, yalnız `ReopenPlanningWeek` eklenmiş olmalı). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Aynı temsilci + dönem için ikinci oluşturma → 409 `planning_session_exists` + mevcut kimlik; arşivli plan engellemez; başka temsilci engellenmez.
2. Hedefsiz ve onaysız plan arşivlenir; hedefli ya da onaylı haftalı plan → 409 `planning_session_not_empty`.
3. `apply` + `weekStart`: yalnız o haftanın atomları yazılır; hafta `approved` + geçmiş kaydı; oturum `committed` değil; ikinci onay → 409 `week_already_approved`; geçmiş hafta → 409 `week_in_past`; Pazartesi değil → 400.
4. Onaylı hafta dondurulur: hedef değişikliğinden sonraki önizleme onaylı haftanın ziyaretlerini değiştirmez ve onları sabit sayar.
5. Yeniden aç: gerekçe < 10 → 400; onaysız hafta → 409; raporlu ziyaret kalır, raporsuz `cancelled (week_reopened)`; hafta `reopened` + geçmişte gerekçe; yeniden onay yalnız eksikleri üretir.
6. `weekStart`'sız `apply` bugünkü gibi (mevcut testler yeşil).
7. Sıklık: haftada 1 × 13 çalışma haftası → 13 · ayda 2 × 3 ay → 6 · dönemde 1 → 1 · bilinmeyen → 1 + `unknown` · eczane politikasız → 1.
8. Dağılım: 13 haftada 2 ziyaret ~6–7 hafta arayla; kalan hesabı yapılan + onaylıyı düşer.
9. Hafta durumu türetme: past / approved / draft / empty sınır günleri (Pazar–Pazartesi geçişi, UTC).
10. Eski belgede `Weeks` alanı yokken okuma ve kayıt çalışır (class-map).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Onayda hafta süzgecini kaldır (tüm haftaları yaz) → test 3 kırmızı.
2. `PeriodType` çarpanını 1'e sabitle → test 7 kırmızı.

### E4 (CT, fleet; kullanıcı girişi; ayrı sekme)
- Yeni dönem planında 42. hafta onayı → yalnız o haftanın ziyaretleri; 43. hafta taslak.
- Yeniden aç (gerekçeli) → raporsuz ziyaretler iptal.
- Aynı dönem için ikinci plan → 409.

Hepsi kayıt işlemi: test planında, kullanıcı onayıyla.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-3A · Dönem planı + hafta durumu (onayla / yeniden aç) + tek plan kuralı + sıklık dağılımı
Repository: C:\tmp\vp-3a (worktree) · Branch: wp/vp-3a · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-3A-period-plan-week-status-frequency.md — önce tamamını oku (Bağlam dosya:satır CT okumasıdır, doğrula). Tasarım: …/DESIGN-VP-FAZ3-planning-engine.md (§2.1–2.3, §4). Ayrıca: services/Diten.CrmService/src/**/Features/VisitPlanning/** · **/Domain/Entities/{PlanningSession,PlannedVisit,VisitFrequencyPolicy,VisitReport}.cs · **/Persistence/Repositories/{PlanningSessionRepository,PlanningSessionApplyUnitOfWork}.cs · Api/Controllers/CRM/VisitPlanningController.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/details.js · tests/architecture/**/AuditTrailStandardTests.cs + audit-ledger/Diten.CrmService.md · .antigravity/rules/audit-trail-standard.md.

NE:
(1) Tek etkin plan: aynı kiracı+temsilci+dönem arşivsiz plan → 409 planning_session_exists (+mevcut id); eski çoklu veri okunur, göç yok; boş (hedefsiz+onaysız) plan Update RequestedStatus=archived, değilse 409 planning_session_not_empty; liste DTO isEmpty.
(2) Hafta modeli: PlanningSession.Weeks[] yalnız onaylı/yeniden açılmış haftalar {WeekStart, Status approved|reopened, ApprovedAt/By, PlannedVisitIds, ManualVisitOrder, History[{At,By,Action,Reason}]}; class-map (string-Guid), eski belge okunur; hafta durumu türetme tek yerde (past/approved/draft/empty, UTC bugün); ayrıntı + önizleme tüm dönem haftalarını durumlarıyla döner.
(3) apply + weekStart = haftayı onayla: yalnız o haftanın atomları, Weeks'e approved + geçmiş, oturum committed OLMAZ; 409 week_in_past / week_already_approved, 400 invalid_week; onaylı haftalar dondurulur (S-1) ve sonraki önizlemede sabit sayılır; weekStart'sız apply bugünkü gibi (deprecate yorumu).
(4) YENİ KOMUT ReopenPlanningWeek: POST sessions/{id}/weeks/{weekStart}/reopen {reason, expectedVersion}; apply izni + VisitOwnership; gerekçe ≥10 (400 reopen_reason_required), 409 week_not_approved / week_in_past; raporsuz/sonuçsuz ziyaretler cancelled (week_reopened), raporlular kalır; hafta reopened + geçmiş; atomik. Mimari test listesiz 26→27 (yalnız bu komut); ledger'a elle satır EKLEME.
(5) Ufuk dönem sonu (MaxWeeks kalkar), başlangıç max(dönem başı, bugünün haftası); gereken = sayı × PeriodType birimi (week=çalışma haftası, month=kesişen ay, period=1); bilinmeyen = 1 + unknown; eczane hesap politikası yoksa 1; kalan = gereken − (yapılan completed + onaylı planlı); kalan taslak haftalara eşit aralık; frequencyStatus/requiredVisitCount slot+aday modeline taşınır.
(6) Web zorunlu uyum: "Bu haftanın planı olarak kaydet" weekStart=seçili hafta ile; kilit yalnız onaylı haftada; yeni metin 7 dil.
KORU/YAPMA: tek yeni komut; göç/seed/grant/indeks YOK; gün dengeleme/kapasite/yarım gün/izin (3B), ürün listesi (3C), durum okumaları (3D) YOK; Rota tasarımı ve mobil sözleşme değişmez (yalnız ek alan); test kayıtları silinmez.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — CRM Application (2347/0/5, PII flake) · Web (733/0) · mimari (38/1, listesiz 26→27 yalnız ReopenPlanningWeek); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–10. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(crm): WP-VP-3A — period plan with per-week approve/reopen, single plan per rep+period, frequency by period type across the whole period" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), hafta durumu kuralı, sıklık birimi/yuvarlama kuralları, iptal alanı/deseni, tekil indeks önerisi, mobil için yeni alanlar, mimari test listesiz komut listesi farkı. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-07)
**Commit:** `e6c383a5b` (ff; taban `eb273412e`). Push: test dalı.

**CT K13:**
| Paket | Taban | Sonuç |
|---|---|---|
| CRM Application | 2347/0/5 | **2369/0/5** (+22) |
| Web | 733/0 | **735/0** (+2) |
| Mimari | 38/1 (26) | **38/1 (27)** — tek fark `ReopenPlanningWeekCommand` ("YENİ KOMUT DENETİMSİZ GELDİ"), F3-1 kabulü; Faz 8'de bağlanır |

**Kod okuması:**
- `PlanningWeekCalendar` (saf; Pazartesi–Pazar, UTC bugün; past → approved → draft / empty).
- `FrequencyExtendPlanner.ResolveRequirementAsync` + `UnitsIn` (week = çalışma haftası, month / quarter = dokunulan, day = çalışma günü, diğer 1) + `Distribute` (k·H / Z).
- Eczane / hesap `account` hedefi olarak çözülüyor. Onaylı hafta `isFixed` ile dondurulup sayılıyor.
- `ReopenWeekAsync` işlem + telafi; iptal mevcut `cancelled` + `CancellationReason = week_reopened`.

**CT sabotajı (ajanınkinden ayrı):** "geçmiş" sınırı `<` → `<=` + dağıtım `k·H/Z` → ardışık haftalar → **2 kırmızı** (`A_week_is_current_through_its_sunday…`, `Two_visits_over_thirteen_weeks…`). Geri alındı.

**Bilinen / takip:**
- Çalışma takvimi artık tüm dönem için gün gün soruluyor (~90 istek / önizleme; eskiden ≤ 42) → 3B'de toplu sorgu ya da önbellek değerlendirilsin.
- Tekil indeks önerisi `{TenantId, CyclePeriodId, ResourceId}` + `partialFilterExpression {Status: {$in: [...]}}`: canlıda 2 grupta 17 fazladan arşivsiz plan var → önce temizlik (veri kararı, kullanıcı).
- Ayrıntı DTO'sunda onaysız haftanın `visitCount`'u boş (yalnız önizleme hesaplar) — Faz 4 önizlemeyi kullanır.
- Geçmişe düşen onaylı hafta `past` görünür (türetme sırası); onay bilgisi `storedStatus` alanında kalır.
- `weekNumber` anlamı değişti (dönem haftası indeksi) → mobil notu (Faz 5).

**E4 (CT, bekliyor; fleet yeniden başlatma — CRM + Web değişti):** yeni dönem planında hafta onayı (yalnız o hafta) · yeniden aç (gerekçeli) · ikinci plan 409. Kayıt işlemleri: test planında, kullanıcı onayıyla. Not: Beste'nin Q4'te zaten iki planı var (a42373cb, a238bdc5) → "ikinci plan 409" doğrudan gözlenebilir.

### §37 ek — E4 (2026-10-07, CT, Beste; kayıtlar kullanıcı onaylı)
- Üçüncü plan oluşturma → 409 `planning_session_exists` + mevcut kimlik; plan sayısı 13'te kaldı ✓.
- Eski taslak `23b1706a`'da 42. hafta onayı → 1 ziyaret (`f08467a9…`), plan `draft` kaldı, hafta `approved` + geçmiş ✓. Tekrar → 409 `week_already_approved`; geçmiş hafta → 409 `week_in_past` ✓. Önizlemede ziyaret `isFixed` ✓.
- "ayda 2" → Q4'te 6; hafta durumları (past / draft / empty) doğru ✓.
- ◐ **Yeniden aç Web'den denenemedi:** Web vekili yok (Faz 4'e bırakılmıştı) → E4 Faz 4'te. 42. hafta onaylı, 1 ziyaretle kalıyor (test kaydı).
- Gözlem: eski `committed` planların önizlemesi yazılmış ziyaretleri saymadan yeniden üretiyor; aşama öngörüsü karışıyor (HALİL ÖZARI 0, 1, 1, 1, 0) → Faz 4'te eski planların gösterimi kararı.
