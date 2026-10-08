# WORK PACKAGE — WP-VP-4A · Faz 4 için backend uyumu: seçili ürünlerin okunması, yeniden aç vekili, eski planların hafta modeline oturması, liste sayıları

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** Faz 3 E4 devirleri ([3A §37 ek](WP-VP-3A-period-plan-week-status-frequency.md), [3C §37 ek](WP-VP-3C-visit-product-list.md)) · [mockup brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) §1 · [yol haritası](ROADMAP-visit-planning.md) Faz 4.
> - **Kullanıcı:** "Faz 4 … devredilen üç maddeyle birlikte paketlemeye başla" (2026-10-07).
> - **Kapsam:** CRM okuma / önizleme uyumu + Web vekili. **Ekran yok** (4B–4D'de).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4a`, dal `wp/vp-4a`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel:** WP-VP-4B (liste + yeni plan paneli + detay üstü) ayrı dalda. Çakışma ihtimali yalnız `frontend/Diten.Web/Controllers/CRM/VisitPlanningController.cs` (bu paket yalnız bir vekil ekler).

## NE
### 1. (E4-3C-B1) Seçili ürünler okunur
- Oturum ayrıntı DTO'su (`GET api/crm/visit-plan/sessions/{id}`) ve `GET …/sessions/{id}/targets` doktor satırlarına `products[{ productId, productCode, productName?, role }]` ekler.
- Kaynak: `SelectedContacts[].Products` (3C). Ürün adı için MDM'yi her okumada çağırma: saklı `productCode` yeterli. Ad gerekirse toplu tek okuma + MDM kapalıysa kodla devam (fail-open, yalnız görüntü).
- Yazma yolu değişmez.

### 2. Yeniden aç Web vekili
- `POST /CRM/VisitPlanning/api/sessions/{id}/weeks/{weekStart}/reopen` → CRM `…/reopen` (3A).
- İzin deseni `apply` vekiliyle aynı. Gövde aynen geçer. 404 / 409 / 400 zarfı bozulmadan döner.

### 3. Eski (`committed`) planlar hafta modeline oturur (F3-2 + Faz 3 E4 gözlemi)
- **Bugün:** tüm dönemi tek seferde uygulanmış eski planın önizlemesi, yazılmış ziyaretleri saymadan yeniden üretiyor. Aşama öngörüsü karışıyor (HALİL ÖZARI 0, 1, 1, 1, 0).
- **CT kararı:**
  - Eski `committed` planda `CommittedPlannedVisitIds`'teki **iptal edilmemiş** ziyaretler **sabit** sayılır (3A'daki onaylı hafta ziyaretleri gibi).
  - Bu ziyaretlerin düştüğü haftalar türetmede `approved` görünür; `storedStatus = legacy`, geçmiş yok.
  - Diğer haftalar: plan zaten "tamamı uygulanmış" olduğundan **taslak üretilmez** (`empty`). Yeni ziyaret istenirse yeni plan modeli Faz 4 sonrası (eski planda yeniden açma yok; F3-2).
- Önizleme bu planlarda yeniden üretmez, sabitleri gösterir. Aşama öngörüsü yalnız gerçek sırayı sayar (çift sayım yok).
- Eski `generated` / `draft` planlar yeni model gibi davranır (3A).

### 4. Liste sayıları (brief §1)
- Liste DTO'suna:
  - `doctorCount`, `pharmacyCount` (seçimden; bugün "Hedefler" hep 0 sorunu D1 düzeltilmişti, değerleri doğrula);
  - `approvedWeekCount` (var);
  - `draftWeekCount`: dönemin kalan (geçmiş olmayan) haftalarından onaylı olmayanlar; üretim yapmadan sayılır — "taslak" burada "onaysız ve geçmemiş" demek, ayrıntıda doğrusu önizlemeden;
  - `resourceDisplayName` (var, doğrula).
- Arşivli planlar listede varsayılan olarak **dönmez** (`includeArchived=true` ile döner). Web listesi zaten gizliyor; API de aynı davranır.

### 5. Detay için "planın haftaları" okuması
- `GET …/sessions/{id}` ayrıntısındaki `weeks[]` (3A) onaysız haftalar için `visitCount` boş dönüyor.
- Haftalar sekmesi önizlemeyi kullanacak (4D). Burada yalnız ayrıntıya `currentWeekStart` (bugünün haftası, dönem içindeyse) ve `nextDraftWeekStart` (bugünden sonraki ilk onaysız hafta) ekle. "Sonraki haftayı aç" düğmesi bunu kullanır.

## KORU / YAPMA
- **Yeni yazma komutu YOK** (mimari listesiz sayı 27 sabit).
- Motorun 3A / 3B / 3C kuralları değişmez; madde 3 yalnız eski `committed` planlara özel dal.
- Mobil yalnız ek alan. Göç / seed / grant / indeks YOK. Test kayıtları silinmez.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM Application (2410/0/5; PII ve ContactWorkbook bilinen kararsız), Web (738/0), mimari (38/1; listesiz 27 sabit). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Ayrıntı ve `targets` doktor satırında `products` döner (seçim saklıysa); seçimsiz doktorda boş liste.
2. Reopen vekili: CRM'e doğru yol ve gövde; 409 zarfı aynen döner.
3. Eski `committed` plan önizlemesi: sabit ziyaretler aynen, yeni ziyaret üretilmez, haftalar `approved` / `legacy`; aşama dizisi yazılmış sıraya uyar (çift sayım yok).
4. Liste: `doctorCount`, `pharmacyCount`, `draftWeekCount`; arşivli plan varsayılan listede yok, `includeArchived=true` ile var.
5. Ayrıntıda `currentWeekStart` / `nextDraftWeekStart` (dönem sınırları ve geçmiş hafta durumları).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Eski plan sabitlerini yok say → test 3 kırmızı.
2. Arşiv süzgecini kaldır → test 4 kırmızı.

### E4 (CT, fleet)
- Plan `a42373cb` önizlemesi: yalnız yazılmış 33 ziyaret, aşamalar düzgün.
- `23b1706a`'da 42. hafta reopen (gerekçeli, kullanıcı onaylı) → ziyaret iptal.
- Listede sayılar.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4A · Faz 4 için backend uyumu: seçili ürünlerin okunması, yeniden aç vekili, eski planların hafta modeline oturması, liste sayıları
Repository: C:\tmp\vp-4a (worktree) · Branch: wp/vp-4a · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-4A-backend-alignment-for-ui.md — önce tamamını oku. Ayrıca: …/WP-VP-3A-period-plan-week-status-frequency.md ve …/WP-VP-3C-visit-product-list.md (§37 + §37 ek) · …/DESIGN-VP-FAZ3-planning-engine.md · services/Diten.CrmService/src/**/Features/VisitPlanning/** · **/Domain/Entities/PlanningSession.cs · Api/Controllers/CRM/VisitPlanningController.cs · frontend/Diten.Web/Controllers/CRM/VisitPlanningController.cs.
NE:
(1) Ayrıntı + sessions/{id}/targets doktor satırına products[{productId,productCode,productName?,role}] (saklı seçimden; MDM her okumada çağrılmaz, ad gerekirse toplu + fail-open).
(2) Web vekili POST /CRM/VisitPlanning/api/sessions/{id}/weeks/{weekStart}/reopen (apply vekili izin deseni, zarf aynen).
(3) Eski committed planlar: CommittedPlannedVisitIds'teki iptal edilmemiş ziyaretler SABİT; düştükleri haftalar approved/storedStatus=legacy; diğer haftalar empty (taslak üretilmez); önizleme yeniden üretmez; aşama öngörüsü çift saymaz; eski generated/draft yeni model gibi.
(4) Liste DTO doctorCount, pharmacyCount, draftWeekCount (üretimsiz: kalan geçmemiş onaysız haftalar), resourceDisplayName doğrula; arşivliler varsayılan listede yok (includeArchived=true ile var).
(5) Ayrıntıya currentWeekStart + nextDraftWeekStart.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz 27 sabit); 3A/3B/3C motor kuralları değişmez (madde 3 yalnız eski committed dalı); ekran yok; mobil yalnız ek alan; göç/seed/grant/indeks YOK.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — CRM Application (2410/0/5; PII + ContactWorkbook bilinen kararsız) · Web (738/0) · mimari (38/1, listesiz 27 sabit); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–5. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(crm,web): WP-VP-4A — plan reads expose picked products, reopen proxy, legacy committed plans as fixed weeks, list counts" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), eski plan dalının kuralı, mobil için yeni alanlar. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-08)
**Commit:** `6ae5a0585` (ff). Push: test dalı.

**CT K13:** CRM 2410 → **2419/0/5** · Web 738 → **742/0** · mimari **27 sabit**.

**Kod okuması:**
- `LegacyCommittedPlan` yalnız okumada; `committed` plan: kayıtlı ziyaretlerden iptal / arşiv olmayanlar sabit, haftaları `legacy` olarak sentezleniyor (saklanmıyor); diğer haftalar `empty`, önizleme üretmiyor.
- Seçili ürünler ayrıntı + `targets` okumasında (saklı kod; MDM çağrısı yok, `productName` null).
- Reopen Web vekili apply izinleriyle.
- Liste: `doctorCount` / `pharmacyCount` / `draftWeekCount`; arşivliler varsayılan listede yok (`includeArchived`).
- `currentWeekStart` / `nextDraftWeekStart`.

**CT sabotajı:** eski planda iptal ziyaret de sabit sayıldı → 1 kırmızı (`An_old_committed_plan_shows_only_its_written_visits…`). Geri alındı.

**Ajan sapması (kabul):** arşivlenmiş ziyaretler de sabit dışı (3A kuralıyla tutarlı).

**Davranış değişikliği:** API listesi arşivlileri artık varsayılan döndürmüyor → mobil notuna (Faz 5).

### §37 ek — E4 ACCEPTED (2026-10-08, CT, fleet, Beste)
- Eski plan `a42373cb`: önizleme yalnız yazılmış **33** ziyaret (hepsi `isFixed`); haftalar `approved/legacy` (19 Eki, 9 Kas), diğerleri `empty`; HALİL ÖZARI aşamaları **0, 1** (karışıklık giderildi) ✓.
- Liste sayıları ("8 doktor · 0 eczane", "1 onaylı · 12 taslak"), arşivliler gizli ✓.
- Reopen vekili 4B ekranından çalıştı (aşağıda) ✓.
