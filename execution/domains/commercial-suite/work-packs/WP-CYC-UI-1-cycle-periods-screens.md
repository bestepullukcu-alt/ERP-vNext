# WORK PACKAGE — WP-CYC-UI-1 · Dönemler ekranları (tablo + yıl zaman çizelgesi, oluştur / düzenle paneli, ayrıntı, geçerli dönem aracı) + dönem kullanım okuması

> **CT (SoR), 2026-10-05.**
> - **Mockup:** `mockups/cycle-planning/cycle-planning-prototype.html` (çözülmüş: `cycle-planning-screens.decoded.html`, ekranlar 01–03). **Analiz + kararlar:** `mockups/cycle-planning/CYCLE-PLANNING-mockup-analysis.md` §2 (K-2, K-3, K-6, K-8, E3–E5, E10, E11), §5 (kullanıcı: tümü kabul).
> - **Önceki:** WP-CAP-MODEL (`9deaca04`) — `GET /api/crm/cycle-periods/code-suggestion`.
> - **Paralel:** WP-CYC-UI-2 (Kapasite ekranları) — bu paketin §Sözleşme'sindeki **kullanım ucunu** sahte gateway'le kullanır.
> - **Kapsam:** `frontend/Diten.Web` (Dönemler) + CrmService'te **tek okuma ucu** (dönem kullanımı).
>
> **Çalışma yeri:** worktree `C:\tmp\cyc-ui-1`, dal `wp/cyc-ui-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kanıt (CT)
- **Bugünkü Web:** `Controllers/CRM/CyclePeriodsController.cs` (`/CRM/CyclePeriods`: liste, Create, Edit, Details), `Views/CRM/CyclePeriods/{Index, _DataTable, _Filter, _Form, Create, Edit, Details, _IndexL10n}.cshtml`, `wwwroot/assets/js/CRM/CyclePeriods/{index.js, form.js, index.l10n.js}`, resx `CyclePeriodsIndex.*`.
- **CRM kuralları (değişmez):** `Domain/Entities/CyclePeriod.cs` (durumlar draft / active / closed, kapsam tenant / country / legal-entity / business-unit, BU kaynağı territory / manual), `UpdateCyclePeriodHandler` (`ScopeImmutable` — kapsam hiç değişmez; `DatesImmutable` — aktifte tarih / yıl / sıra / kapsam değişmez), `CreateCyclePeriodHandler` (kod elle, küçük harfe çevrilir, tekil, değişmez; `SequenceTaken` kapsam + yıl içinde, kapalılar dahil), `CyclePeriod.cs:51-52` (bitiş > başlangıç), `CyclePeriodOverlapRules` (yalnız aynı kapsamda aktif çakışma yasak), `CyclePeriodResolveEngine` (yalnız aktif; resolved / none / ambiguous), etkinleştir / kapat komutları (taslak → kapalı da var).
- **Dönem bağları:** `PlanningSession.CyclePeriodId` (zorunlu), `Campaign.CyclePeriodId?`; `PlannedVisit`'te dönem alanı **yok** — dönemle bağ, oturumun `CommittedPlannedVisitIds`'i üzerinden.
- **Seçenek kaynakları:** `GetCyclePeriodScopeOptionsHandler` (ülke / tüzel kişi / iş birimi), çalışma takvimi iş günü sayımı (`IWorkingDayCounter`).

## NE
### 1. CRM — dönem kullanım okuması (yalnız okuma)
`GET /api/crm/cycle-periods/{id}/usage` (yetki: dönem okuma; tenant):
- `capacity`: `{cycleCapacityId, isArchived}` ya da `null`;
- `campaigns[]`: `{campaignId, code, name, status}` (`Campaign.CyclePeriodId == id`);
- `planningSessions[]`: `{planningSessionId, name, ownerDisplayName?, status, committedVisitCount}`;
- `plannedVisits`: `{total, byStatus{draft, planned, confirmed, cancelled, …}}` — oturumların bağlı ziyaretlerinden;
- `demandByMonth[]`: `{year, month, plannedVisits}` (iptal / arşiv hariç) — WP-CYC-UI-2'nin arz / talep grafiği;
- kişisel veri yok (ad yalnız sahip için, `IUserDisplayNameResolver`).
- Liste ucuna (varsa) satır başına `hasCapacity`, `campaignCount`, `plannedVisitCount` ek alanları — **toplu** okuma (satır başına ayrı sorgu yok); eklenemiyorsa Web ayrıntıda gösterir, listede "kapasite var mı" yalnız kapasite listesinden çıkarılır.

### 2. Web — Dönemler (mockup ekran 01–03, kararlarla)
- **Liste:** iki görünüm — tablo ve **yıl zaman çizelgesi** (kapsam başına satır, durum renkleri, boşluk / çakışma vurgusu, bugün çizgisi). **Eksen dinamik** (filtredeki yıl ± 1; E10). Sütunlar: kod, ad, yıl / sıra, başlangıç – bitiş, gün, kapsam (+ değer), durum, kapasite (✓ / "kapasite yok"), kampanya, planlanan ziyaret. Filtreler: yıl, kapsam türü, ülke, durum. "Kapasitesi olmayan açık dönemler" bandı (E11).
- **Oluştur / düzenle (sağ panel):**
  - Kimlik: yıl, sıra (öneri), ad (öneri "2026 · 4. dönem"), **kod — `code-suggestion` ucundan öneri, kullanıcı değiştirebilir, kayıttan sonra salt okunur** (K-2), açıklama.
  - Tarihler: hızlı şablonlar (çeyrek / yarıyıl / ay), canlı gün + iş günü (takvim durum rozetiyle), **bitiş > başlangıç** (E4).
  - Kapsam: tek seçim + alt alanlar (ülke; tüzel kişi + ülkesi bilgi; iş birimi + kaynak + ülke bağlamı). **Kapsam yalnız oluştururken seçilir** (sonra salt okunur).
  - Kurallar ekranda: aynı kapsamda aktif çakışma uyarısı (çakışan döneme bağlantı), farklı düzey bilgisi, **sıra tekil** anlık uyarı (E5).
  - **Aktif dönem:** tarih / yıl / sıra / kapsam salt okunur + "Kapat ve yeni dönem aç" yönlendirmesi (K-3); ad ve açıklama düzenlenebilir.
- **Ayrıntı:** özet, durum çizgisi, kim / ne zaman etkinleştirdi / kapattı; **eylemler: taslakta Etkinleştir + Kapat (E3), aktifte Kapat** (onaylı); kapalı tamamen salt okunur. Bağlı kayıtlar (`usage`): kapasite kartı (yoksa "Kapasite oluştur"), kampanyalar, planlama oturumları, planlanan ziyaretler (durumlara göre); takvim özeti (ay ay iş günü / tatil).
- **"Geçerli dönem bul" aracı:** birim (ülke / tüzel kişi / iş birimi) + tarih → mevcut çözüm ucu; **yalnız aktifler**; sonuç `resolved / none / ambiguous` ayrı anlatımla (K-6).
- **Durumlar:** boş, yükleniyor, hata, yetkisiz (iskelet yok, yönlendirme yok), kapalı dönem, takvim çözülemedi.
- **Genel:** 7 dil (en, tr, fr, es, zh, ar, ru) — TR diakritik, Arapça sağdan sola (zaman çizelgesi aynalanır), tema değişkenleri (sabit hex yok), klavye (panel Esc + odak tuzağı, radyo grubunda ok tuşları), sayı / tarih yerel biçim. Altın liste şablonu (tablo görünümü) sapmasız.

## KORU / YAPMA
- CRM dönem / kapasite **yazma** kuralları DEĞİŞMEZ (yalnız okuma ucu eklenir).
- Kapasite ekranları DOKUNMA (WP-CYC-UI-2). Menü yerleri aynı.
- `esc()` / `textContent`; proxy 204 tuzağı.
- **DUR:** kullanım okuması planlanan ziyaret sayılarını oturum bağı dışında bir yolla gerektiriyorsa; altın liste şablonu zaman çizelgesi görünümüne izin vermiyorsa → raporla.

## Sözleşme (WP-CYC-UI-2 ile paylaşılan)
```
GET /api/crm/cycle-periods/{id}/usage
→ { capacity: {cycleCapacityId, isArchived} | null,
    campaigns: [{campaignId, code, name, status}],
    planningSessions: [{planningSessionId, name, ownerDisplayName, status, committedVisitCount}],
    plannedVisits: {total, byStatus: {<status>: n}},
    demandByMonth: [{year, month, plannedVisits}] }
404 dönem yok / başka kiracı
```

## Acceptance
- **E2:** CRM 0 kırmızı (taban ölç; son CT 2223/0/5), Web 0 kırmızı (taban 473), build 0 hata.
  - **Yeni testler:** kullanım ucu (kampanya / oturum / ziyaret sayıları, iptal-arşiv hariç aylık talep, kiracı izolasyonu, kişisel veri yok); Web: zaman çizelgesi verisi (dinamik eksen), kod önerisi + kayıttan sonra salt okunur, aktif dönemde yapısal alanlar salt okunur, taslakta Kapat eylemi, sıra tekil uyarısı, bitiş > başlangıç, geçerli dönem aracı üç sonuç, yetkisizde iskelet yok, 7 dil anahtar eşitliği.
  - **Sabotaj:** (1) kullanım ucunda iptal filtresini kaldır → talep testi kırmızı; (2) aktif dönemde tarih alanını düzenlenebilir yap → Web testi kırmızı.
- **E4 (CT):** canlıda zaman çizelgesi + ayrıntıda bağlı kayıtlar.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-CYC-UI-1 · Dönemler ekranları (tablo + yıl zaman çizelgesi, oluştur/düzenle paneli, ayrıntı, geçerli dönem aracı) + dönem kullanım okuması
Repository: C:\tmp\cyc-ui-1 (worktree) · Branch: wp/cyc-ui-1 · commit bu dala, push YOK · frontend/Diten.Web (Dönemler) + CrmService'te tek okuma ucu

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-CYC-UI-1-cycle-periods-screens.md — önce tamamını oku (Kanıt / NE / KORU / Sözleşme / Acceptance). Mockup: …/mockups/cycle-planning/cycle-planning-screens.decoded.html (ekran 01–03) + prototip HTML; kararlar …/mockups/cycle-planning/CYCLE-PLANNING-mockup-analysis.md §2 + §5. Ayrıca: frontend/Diten.Web/{Controllers/CRM/CyclePeriodsController.cs, Views/CRM/CyclePeriods/**, wwwroot/assets/js/CRM/CyclePeriods/**} · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{CyclePeriod, PlanningSession, Campaign}.cs · Application/Features/CyclePeriod/** · .antigravity/rules altın liste şablonu · memory l10n-bridge-pascalcase-loader, proxy-forward-204-content-length-crash, dt-inline-filter-host-class, updatevisualstate-global-selectors.

NE: (1) CRM GET /api/crm/cycle-periods/{id}/usage (WP §Sözleşme; tenant; ziyaretler oturum CommittedPlannedVisitIds üzerinden; iptal/arşiv hariç aylık talep; kişisel veri yok); listeye toplu hasCapacity/campaignCount/plannedVisitCount (olmuyorsa raporla). (2) Web Dönemler: tablo + yıl zaman çizelgesi (dinamik eksen, kapsam satırları, boşluk/çakışma, bugün), filtreler, "kapasitesi olmayan açık dönemler" bandı; sağ panel oluştur/düzenle: kod code-suggestion'dan öneri (kayıttan sonra salt okunur), hızlı tarih şablonları, canlı iş günü, bitiş>başlangıç, kapsam yalnız oluşturmada, aktif çakışma uyarısı (bağlantılı), sıra tekil uyarısı, aktif dönemde yapısal alanlar salt okunur + "kapat ve yeni aç"; ayrıntı: durum çizgisi, Etkinleştir/Kapat (taslakta da Kapat), bağlı kayıtlar (usage), takvim özeti; "geçerli dönem bul" (yalnız aktif, resolved/none/ambiguous). Durumlar; 7 dil + RTL (zaman çizelgesi aynalı) + tema değişkenleri + klavye (Esc, odak tuzağı, ok tuşları) + yerel sayı/tarih.
KORU/YAPMA: CRM dönem/kapasite yazma kuralları değişmez; kapasite ekranlarına DOKUNMA (CYC-UI-2); esc()/textContent; 204 tuzağı; tablo görünümü altın şablondan sapmasız.
DOĞRULA (E2): CRM testleri (tabanı ölç; son CT 2223/0/5) → 0 kırmızı (PiiMasking flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 473); build 0 hata. Yeni testler WP Acceptance. Sabotaj: (1) usage'da iptal filtresini kaldır → kırmızı; (2) aktif dönemde tarihi düzenlenebilir yap → kırmızı; geri al. TestResults/*.trx izleniyor, klasörü silme. Commit ("feat(web): WP-CYC-UI-1 — cycle periods screens (table + year timeline, side panel form, details, effective-period tool) + period usage read" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: usage ziyaret sayısını oturum bağı dışında gerektiriyorsa; altın şablon zaman çizelgesine izin vermiyorsa → DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-05) — **ACCEPTED (E2)**
- **Commit:** ajan `0617bd31` → CYC-UI-2 (`8ad19675`) üzerine rebase (çakışmasız) → `7eb58d3f` → `test/crm-content-visit-e2e` fast-forward. 39 dosya (+4497 / −898).
- **CRM:** `GET cycle-periods/{id}/usage` dar salt okunur `ICyclePeriodUsageReader` üzerinden (depo arayüzleri değişmedi); ziyaretler oturum `CommittedPlannedVisitIds` üzerinden; iptal + arşiv talepte yok; liste satırlarına toplu `hasCapacity / campaignCount / plannedVisitCount` (okunamazsa boş, 0 değil). Oturumun ad alanı yok → `name` = plan haftası / oluşturulma günü.
- **Web:** tablo + yıl zaman çizelgesi (eksen seçili yıl ± 1, RTL aynalı), sağ panel form (kod önerisi + kayıttan sonra salt okunur, aktifte yapısal alanlar kilitli + "kapat ve yeni aç"), ayrıntı (taslakta Kapat dahil), geçerli dönem bulucu (resolved / none / ambiguous), kapasitesiz açık dönem bandı; kurallar C# `CyclePeriodScreenRules`'ta (test edilir), JS çizer; 110 anahtar × 7 dil; yetkisize düz 403.
- **Bilinen sapma (kullanıcı kararı, ajan oturumu):** form ayrı Create / Edit sayfası değil, sağ panel (Compact kuralından bilinçli sapma).
- **CT testleri (CYC-UI-2 ile birleşik):** CRM **2231/0/5**, Web **536/0**.
- **CT sabotajı:** talepte arşiv dışlaması kaldırıldı → 2 kırmızı. Kod geri alındı. Ajan: iptal filtresi (2), aktif dönemde tarih düzenlenebilir (1).
- **Açık:** liste altın şablon denetimi 12 sapma (hepsi önceden var) — kullanıcı kararı bekliyor.
- **WP-CYC-UI-FIX-1 güncellemesi (2026-10-05):** liste altın şablon sapmaları **1–3 düzeltildi** — veri modu `data-dt-data-mode="client"` (tüm küme bir kez yüklenir; sayfalama / sıralama / filtre tarayıcıda), ortak `<partial name="_TableSkeleton" />`, `#offcanvasDetailsPreview` hızlı bakış (satır verisinden, ek istek yok, düz metin). **Bilinen sapmalar** (doğrulayıcı `--api-profile proxy` ile kalan 8 madde; `CyclePeriodsController` belge yorumunda da kayıtlı):
  - ortak `personalization-client.js` kiracı başlığı kontrolü — modül dışı ortak dosya, ayrı iş;
  - doğrulayıcının varsayılan profilde doğrudan gateway (`window.API`) beklentisi — sayfa bilinçli aynı köken proxy kullanır (proxy profiliyle bu madde raporlanmaz);
  - tümünü seç sütunu, toplu işlem yapılandırması, toplu seçim bağlama, `/bulk` ucu, toplu silme tetikleyicisi, `reloadWithToast`, seçimi temizle — modülde silme yok (sonlandırma Kapat / Arşivle), toplu yüzey yok.
- **E4:** CT, fleet sonrası.

### §37 ek — E4 (CT, canlı, 2026-10-06) — **ACCEPTED (E4) + bulgular**
- Fleet güncel dalla, kullanıcı oturumu; CT yerleşik tarayıcıda. Hiçbir kayıt yazılmadı.
- ✓ Liste (geniş ekranda tüm sütunlar; dar ekranda duyarlı katlama), satır `tr-2026-q4`: 01 Eki – 31 Ara 2026, 92 gün, Ülke TR, Aktif, kapasite ✓, 0 kampanya, 176 planlanan ziyaret (`usage` + liste toplu alanları doğru).
- ✓ Zaman çizelgesi (TR satırı, durum renkleri, açıklama, bugün çizgisi), ✓ hızlı bakış (tarih, kapsam, durum çizgisi, kapasite, sayılar), ✓ geçerli dönem bul (Ülke + TR + bugün → `tr-2026-q4`, düzey Ülke).
- **Bulgular (→ WP-CYC-UI-FIX-2):**
  1. Üst "Bugün geçerli dönem" kutusu birim vermeden çözüm istiyor → "aktif dönem yok" diyor; oysa bugünü kapsayan aktif TR dönemi var (yanıltıcı).
  2. Zaman çizelgesi açılışta bugüne kaydırılmıyor (eksen başı 2025; 2026 Q4 en sağda).
  3. Yükleme metni "Yukleniyor..." (TR diakritik eksik); bulucuda ülke adları İngilizce ("Turkey").
