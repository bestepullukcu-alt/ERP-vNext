# WORK PACKAGE — WP-VP-3D · Hedef durum okumaları: doktor başına hedef / yapılan / kalan / son ziyaret, toplu okumalar (D5), segment rozeti

> **CT (SoR), 2026-10-07.**
> - **Tasarım:** [DESIGN-VP-FAZ3](DESIGN-VP-FAZ3-planning-engine.md) §2.6 · [yol haritası](ROADMAP-visit-planning.md) Faz 3 (B-6, B-9, D5).
> - **Kullanıcı:** "Faz 3 … paketle" (2026-10-07).
> - **Kapsam:** yalnız CRM **okuma** uçları + Web vekilleri. Ekran Faz 4'te; bugünkü Details istek sayısı istenirse bu uçlara geçirilir (madde 4).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-3d`, dal `wp/vp-3d`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel:** WP-VP-3A ayrı dalda (oturum / motor). Bu paket motora ve oturum yazmasına **dokunmaz**. Çakışma ihtimali `VisitPlanningModels.cs` (yeni DTO'ları **ayrı dosyaya** koy).

## Bağlam (CT kod okuması, 2026-10-07)
- **Doktor başına "yapılan / kalan / son ziyaret" veren okuyucu yok.**
  - `VisitReport`'ta `ContactId` yok; bağ yalnız `PlannedVisitId` (`Domain/Entities/VisitReport.cs:24`).
  - Raporun `ExecutionOutcome` :28, `ExecutedAt` :58, `IsCompleted()` :80.
  - Depoda iletişim (contact) bazlı sorgu yok (`IVisitReportRepository.cs:21–29`: `GetByPlannedVisitIdAsync`, `ListByPlannedVisitIdsAsync`).
- `PlannedVisit` atomlarında sıklık damgası var: `FrequencyStatus`, `RequiredVisitCount`, `PeriodType` (`Application/Features/PlannedVisit/Provenance/PlannedVisitFrequencyProbe.cs:21–48`).
- `PlannedVisitStatus`'ta "completed" yok (draft / planned / confirmed / cancelled / archived, `PlannedVisit.cs:380–388`). Tamamlanma rapordan anlaşılır.
- Bölge hazırlığındaki `LastVisitDate` / `DueStatus` hiç hesaplanmıyor (`TerritoryReadinessHandlers.cs:255–262, 323`).
- **Sıklık çözümü:** `VisitFrequencyPolicy` resolve (`Application/Features/VisitFrequencyPolicy/Resolve/*`) ve `PlannedVisitFrequencyProbe`.
- **Segment:** doktorun aktif segmentleri tek-kişi değerlendirmesiyle (`SegmentMembershipResolver.EvaluateAsync`; WP-VP-2 `VisitProvenanceDeriver` aynısını kullanıyor).
- **D5 — Details açılışı:** `frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/details.js`.
  - Kayıtlı her hesap ve eczane için `GET /accounts/{id}` (:1114, :1132, :1135).
  - Her hesap için `related-accounts` (:1140, işlev :979–992).
  - İlk hesap için `contacts?pageSize=500` (:1083).
  - Gün bloklarında `fetchAccountContacts` (:600–604, :697).
  - Sonuç: hesap başına 2–3 istek.
- `my-accounts` + `RepTerritoryCoverage` (WP-VP-2B) ve `AccountActiveContacts` hazır.

## NE
### 1. Doktor başına dönem durumu (B-6) — paylaşılan okuyucu
Tek bir uygulama servisi: `ContactPeriodStatusReader`. Girdi: kiracı, temsilci, dönem, doktor kimlikleri (toplu). Her doktor için:
- `requiredVisitCount` + `frequencyStatus` (resolved / unknown / conflict) + `periodType`. Sıklık kuralı 3A ile **aynı** olmalı. 3A birleşmeden önce bugünkü çözücüyü kullan, kural yerini raporla. 3A gelince tek kurala bağlanır; ortak işlev adı öner.
- `done`: o dönemde temsilcinin (sahiplik) planlı ziyaretleri arasında raporu `completed` olanlar. Planlı ziyaretler doktora göre toplu okunur, raporlar `ListByPlannedVisitIdsAsync` ile toplu okunur. **Satır başına sorgu yok.**
- `planned`: iptal / arşiv dışı, sonucu olmayan, tarihi bugün ve sonrası olan planlı ziyaretler.
- `remaining` = max(0, required − done − planned).
- `lastVisitDate`: en son `completed` raporun `ExecutedAt` tarihi, yoksa null.
- `neverVisited`: hiç `completed` yok.
- `dueThisWeek`: kalan > 0 ve eşit dağılıma göre bu hafta vadesi gelmiş. Kural: son ziyaretten ya da dönem başından bu yana geçen hafta ≥ dönem haftası / gereken. Belgele.
- `segmentBadges`: aktif segment adları (bilgi; K-4). Tek kişi değerlendirmesi pahalıysa toplu yolu raporla. Doktor başına en çok N segment.
- `consentStatus`, `inactive` (mevcut alanlardan).

### 2. Uçlar (yalnız okuma; yeni yazma komutu YOK)
- **`GET api/crm/visit-plan/my-accounts/{accountId}/doctors?planningSessionId=&quick=due|never|all&search=&specialty=`**
  - Kurumun aktif doktorları (WP-VP-2B aktif bağlantı kuralı) + madde 1 sütunları.
  - Sahiplik: kurum temsilcinin kapsamında değilse `outOfTerritory = true` döner, gizlenmez (K-5 / bölge dışı ekleme).
  - Arama Türkçe duyarsız (`TurkishInsensitivePattern`).
  - Sayfalama, kurum başına üst sınır (ör. 500).
- **`GET api/crm/visit-plan/sessions/{id}/targets`**
  - Planın seçili kurum, eczane ve doktorları: ad, tür, şehir / ilçe, koordinat; doktor için madde 1 sütunları.
  - **Tek istek** (D5). Sahiplik kuralı mevcut oturum okumasıyla aynı.
- **`GET api/crm/accounts/related?accountIds=a,b,c&relationType=pharmacy`**
  - Toplu ilişkili hesaplar (bugünkü tekil `related-accounts`'ın toplu karşılığı).
  - `crm.account.read`. En çok 100 kimlik; fazlası 400 `too_many_ids`.
- **Önizlemeye ek alan:** her slot ve doktor içerik özetinde `frequencyStatus` + `requiredVisitCount`.
  - Motor bugün bunları atıyor (`VisitPlanningEngine.cs:342–349`). 3A taşıyacak; bu pakette **yalnız DTO alanı** + 3A yoksa geçici olarak probe'dan doldurma.
  - Motor çakışmasını önlemek için motora dokunma, alanı boş bırak ve raporla.
- **Web vekilleri:**
  - `GET /CRM/VisitPlanning/api/my-accounts/{accountId}/doctors`
  - `GET /CRM/VisitPlanning/api/sessions/{id}/targets`
  - `GET /CRM/VisitPlanning/api/accounts/related`
  - İzinler mevcut desenle (okuma izni).

### 3. Mobil
- Üç uç da mobil için kullanılabilir (aynı zarf, `X-Tenant-Id`).
- Mobil notuna (`mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md`) **ek bölüm**: alan adları, örnek yanıt.

### 4. Web istek sayısı (D5) — isteğe bağlı küçük adım
- `details.js` açılışında hesap başına `GET /accounts/{id}` + `related-accounts` yerine `sessions/{id}/targets` + `accounts/related` kullanılır.
- **Ekran görünümü değişmez** (Faz 4 tasarımı değiştirecek).
- Ölç: açılıştaki istek sayısı önce → sonra (ör. `a42373cb` planı, Playwright yoksa kaynak testinde çağrı sayısı).
- Risk görürsen bu maddeyi bırak, raporla (Faz 4'te yapılır).

## KORU / YAPMA
- **Yalnız okuma.** Yeni yazma komutu YOK (mimari test listesiz sayısı bu paketle **değişmez**).
- Motor (`VisitPlanningEngine`, `FrequencyExtendPlanner`) ve oturum yazma yolu **3A'nın**. Dokunma.
- Kiracı / sahiplik: her okuma kiracıyla sınırlı; başka temsilcinin planı 404 (WP-VP-2 kuralı).
- Ekran tasarımı değişmez. Yeni metin varsa 7 dil. Seed / grant / göç / indeks YOK. Gerekirse indeks önerisini raporla.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM Application (2347/0/5; PII flake), Web (733/0), mimari (38/1; listesiz **26 sabit** — 3A'dan önce birleşirse; sonra 27). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Doktor durumu: `completed` raporlu 1 + gelecekte planlı 1 + gerekli 3 → done 1, planned 1, remaining 1; `lastVisitDate` = rapor `ExecutedAt`.
2. Başka temsilcinin planlı ziyaretleri sayılmaz (sahiplik); başka kiracınınkiler hiç okunmaz.
3. İptal / arşiv ziyaret sayılmaz; `missed` sonuçlu ziyaret `done` değildir.
4. `quick=due / never / all` süzgeçleri; `dueThisWeek` kuralı sınır durumları.
5. Okuma sayısı sabit: 5 doktor ve 50 doktor için aynı sayıda depo okuması (doktor başına sorgu yok).
6. `sessions/{id}/targets` tek yanıtta kurum / eczane / doktor adları + durumlar; yabancı plan 404.
7. `accounts/related` toplu: 3 kurum → ilişkili eczaneler; > 100 kimlik → 400 `too_many_ids`.
8. Arama "şirin" → "ŞİRİN" (Türkçe duyarsız).
9. (Madde 4 yapıldıysa) Details açılışında hesap başına `GET /accounts/{id}` çağrısı yok (kaynak testi).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Sahiplik süzgecini kaldır → test 2 kırmızı.
2. `missed`'i `done` say → test 3 kırmızı.

### E4 (CT, fleet; salt okuma)
- Memorial Şişli doktorları: SADAKAT ÖZDİL `done 1` (rapor `939766be`), son ziyaret 5 Eki.
- `targets` tek istek.
- Details açılışında istek sayısı (madde 4 yapıldıysa).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-3D · Hedef durum okumaları: doktor başına hedef/yapılan/kalan/son ziyaret, toplu okumalar (D5), segment rozeti
Repository: C:\tmp\vp-3d (worktree) · Branch: wp/vp-3d · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-3D-target-status-reads.md — önce tamamını oku (Bağlam dosya:satır CT okumasıdır, doğrula). Tasarım: …/DESIGN-VP-FAZ3-planning-engine.md (§2.6). Ayrıca: services/Diten.CrmService/src/**/Features/{VisitPlanning/MyAccounts,VisitReport,PlannedVisit/Provenance,VisitFrequencyPolicy/Resolve,Segmentation/Resolution,Account}/** · **/Domain/Entities/{VisitReport,PlannedVisit}.cs · **/Domain/Repositories/{IVisitReportRepository,IPlannedVisitRepository}.cs · Api/Controllers/CRM/{VisitPlanningController,AccountController}.cs · frontend/Diten.Web/Controllers/CRM/VisitPlanningController.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/details.js · mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md.

NE:
(1) Paylaşılan ContactPeriodStatusReader (toplu): requiredVisitCount+frequencyStatus+periodType (3A ile aynı kural; 3A öncesi bugünkü çözücü, yeri raporla), done (temsilcinin dönem planlı ziyaretlerinden raporu completed), planned (iptal/arşiv dışı sonuçsuz bugün+), remaining, lastVisitDate (son completed ExecutedAt), neverVisited, dueThisWeek (kuralı belgele), segmentBadges (aktif segment adları, bilgi), consentStatus, inactive; doktor başına sorgu YOK.
(2) Uçlar (yalnız okuma): GET api/crm/visit-plan/my-accounts/{accountId}/doctors?planningSessionId&quick=due|never|all&search&specialty (aktif bağlantı kuralı, outOfTerritory işareti, Türkçe arama, sayfalama) · GET api/crm/visit-plan/sessions/{id}/targets (tek istekte kurum/eczane/doktor + durumlar; sahiplik) · GET api/crm/accounts/related?accountIds&relationType (≤100, 400 too_many_ids) · önizleme DTO'suna frequencyStatus/requiredVisitCount alanı (motora DOKUNMA, 3A dolduracak) · Web vekilleri.
(3) Mobil notuna ek bölüm (alanlar + örnek yanıt).
(4) İsteğe bağlı: details.js açılışı targets + accounts/related'a geçer, görünüm değişmez, istek sayısı önce/sonra; risk varsa bırak, raporla.
KORU/YAPMA: yalnız okuma, YENİ YAZMA KOMUTU YOK; motor ve oturum yazma yolu 3A'nın (dokunma); kiracı+sahiplik her okumada; yeni DTO'lar ayrı dosyada; tasarım değişmez; yeni metin 7 dil; seed/grant/göç/indeks YOK.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — CRM Application (2347/0/5, PII flake) · Web (733/0) · mimari (38/1, listesiz sayı DEĞİŞMEZ); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–9. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(crm,web): WP-VP-3D — per-doctor period status (required/done/remaining/last visit), plan targets and related accounts in bulk reads" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), dueThisWeek kuralı, okuma sayıları, segment rozeti maliyeti, 3A ile birleşecek sıklık kuralının yeri, D5 istek sayısı önce/sonra, mobil not eki. §22 TÜRKÇE. K13.
```
