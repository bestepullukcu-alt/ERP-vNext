# WORK PACKAGE — WP-VP-4M · Hedefler sayıları: planlı ziyaret, plandaki doktorlar, seçili hafta (backend + Web)

> **CT (SoR), 2026-10-09.** Kullanıcıyla canlı test (plan `f2c6014d`, 42. hafta, Acıbadem Taksim: 29 doktor, 4'ü planda). Kullanıcı: "evet bunlardı, 4L-BE dönünce paketle."
>
> **Görülen (4L öncesi ölçüm):**
> 1. "Yapılan / kalan" planlı ziyareti göstermiyor.
>    - Plandaki doktorda sunucu: gereken 1, yapılan 0, **planlı 1** (41. hafta onaylı), kalan 0.
>    - Ekran "0 / 0" yazıyor, sanki hedef yokmuş gibi.
> 2. Kurum kartı "4 / 29 seçili" diyor, ama varsayılan "Bu hafta görülmesi gerekenler" süzgeci bu 4 doktoru gizliyor; tabloda seçili kimse yok.
> 3. "Bu hafta" iki farklı anlamda:
>    - `dueThisWeek`, satır rozeti ve süzgeç sayıları **bugünkü haftaya** göre;
>    - başlık ("0 ziyaret bu hafta") **seçili haftaya** göre.
>
> **4L sonrası:** tanımsız sıklık = haftada 1 (gereken = dönemin çalışma haftası). 3D okumaları zaten `?weekStart=` alıyor (4L-BE), şimdilik yalnız `extraThisWeek` için.
>
> **İki bölüm, paralel:**
> - 4M-BE: `C:\tmp\vp-4m-be`, dal `wp/vp-4m-be`;
> - 4M-WEB: `C:\tmp\vp-4m-web`, dal `wp/vp-4m-web`.
> - İkisi de test dalı başından. Commit dala, push YOK.

## 4M-BE — CRM (3D durum okumaları)

### 1. "Bu hafta" = seçili hafta
- `GET my-accounts/{id}/doctors` ve `GET sessions/{id}/targets` `?weekStart=yyyy-MM-dd` (4L-BE'de eklendi) verildiğinde, `dueThisWeek` **o haftaya göre** hesaplanır: `IsDueThisWeek(..., reference = weekStart)`. Verilmezse bugün (eski davranış).
- `quick=due` süzgeci ve yanıttaki sayılar aynı referansla. Hızlı süzgeç sayıları yanıtta ek alan **`quickCounts { due, never, all }`** — Web'in sayıları ayrı istek / tahminle üretmemesi için. Yoksa ajan mevcut sayım yolunu raporlar.
- **Hafta geçerliliği:** dönem dışındaki ya da Pazartesi olmayan `weekStart` → `400 invalid_week`. Geçmiş hafta kabul (salt okuma).
- **4L kuralıyla tutarlılık:**
  - tanımsız sıklık (haftalık varsayılan) olan doktor, kalan > 0 ise ve aralık kuralı tutuyorsa **due** olur.
  - `IsDueThisWeek` bugün "sıklık bilinmiyorsa due değil" diyor. 4L'den sonra tanımsız sıklığın gereken sayısı var; bu dal artık yalnız gerçekten çözülemeyen (`required == null`) durum için.
  - Test: haftalık varsayılan doktor, o hafta ziyareti yoksa due.

### 2. Planlı sayısı seçili haftaya göre ayrışsın
- Yanıt durumuna ek alan **`plannedThisWeek: bool`**: seçili haftada planlı ya da onaylı ziyareti var mı (önizleme yok; planlanan ziyaret kayıtlarından). Seçili hafta verilmezse null.
- **Not:** taslak haftanın ziyaretleri kayıt değil, önizlemede. Web taslak hafta için önizlemeden okur (4L-WEB "Bu hafta planlı"). `plannedThisWeek` yalnız yazılmış ziyaretleri söyler; açıklamaya yazılır.

### Acceptance (4M-BE)
1. `weekStart` = 42. hafta → `dueThisWeek` 42. haftaya göre; aynı doktor `weekStart` yokken bugüne göre (iki farklı sonuç senaryosu).
2. Haftalık varsayılan doktor: 42. haftada ziyareti yoksa ve kalan > 0 → due.
3. `quickCounts` süzgeçle tutarlı; `quick=due` listesi = `quickCounts.due` sayısı.
4. Geçersiz `weekStart` → 400.
5. `plannedThisWeek` yazılmış ziyarette true, olmayanda false, parametresiz null.
6. Mevcut 3D / 4L testleri yeşil.
- **Sabotaj:** referansı yine bugüne sabitle → test 1 kırmızı.

## 4M-WEB — Hedefler

### 3. "Yapılan / kalan" planlıyı da göstersin
- Hücre:
  - üst satır **"yapılan / gereken"** (ör. "0 / 13");
  - alt satır küçük: **"1 planlı · 12 kalan"**.
- Sıklık hiç yoksa (gereken null) "—".
- `ratio()` RTL yalıtımı korunur (4I).
- İpucu (title): "Yapılan: raporlu ziyaret · Planlı: raporu henüz olmayan planlı ziyaret · Kalan: gereken − yapılan − planlı".
- Sütun başlığı aynı kalır ("Yapılan / kalan").

### 4. Plandaki doktorlar her zaman görünsün
- Seçili kurumun **planda olan** doktorları, hızlı süzgeçten bağımsız olarak tablonun **başında** durur; süzgeç yalnız planda olmayanlara uygulanır.
- İnce bir ara başlık: "Planda (4)" / "Diğer doktorlar".
- Durum sütununda 4L etiketleri ("Bu hafta planlı / Ek ziyaret / Bu hafta yok").
- Kurum kartındaki "x / y seçili" ile tablo artık çelişmez.
- "Tümünü seç (N)" yalnız süzgece uyan **planda olmayan** seçilebilirleri sayar (mevcut davranış korunur).

### 5. Seçili haftayı gönder
- Doktor (`my-accounts/{id}/doctors`) ve plan hedefleri (`sessions/{id}/targets`) okumalarına **`weekStart` = seçili hafta** eklenir.
- Hafta değişince bu okumalar yenilenir (önbellek anahtarı hafta dahil).
- Hızlı süzgeç sayıları yanıttaki `quickCounts`'tan; yoksa bugünkü yöntem.
- Satırdaki "Bu hafta" (görülmeli) rozeti seçili haftaya göre gelir. Metin: "**Bu hafta görülmeli**" ("bu hafta" seçili haftayı anlatır). Başlıkla aynı hafta.

### Acceptance (4M-WEB)
1. Hücre "0 / 13" + "1 planlı · 12 kalan"; gereken yoksa "—"; RTL yalıtımı.
2. Plandaki doktorlar süzgeçten bağımsız başta, ara başlıklarla; süzgeç sayıları yalnız diğerlerine.
3. Okumalar `weekStart` gönderiyor; hafta değişince yenileniyor.
4. `quickCounts` varsa sayılar ondan.
5. Yeni metinler 7 dilde.
- **Sabotaj:** plandaki doktoru süzgeçle gizle → test 2 kırmızı.

## KORU / YAPMA (iki bölüm)
- Yeni yazma komutu YOK (yalnız okuma; listesiz 27).
- 3A / 3D / 4L kuralları (sıklık, haftalık varsayılan, ek ziyaret) aynen; yalnız referans hafta ve gösterim.
- Mockup v3 Hedefler tasarımı, eşit yükseklik düzeltmesi, 4J / 4L davranışları aynen. UAS-001.
- 7 dil. Kiracı sınırı. Mobil yalnız ek alan (`quickCounts`, `plannedThisWeek`).
- Canlı veriye yazma yok. Kullanıcının sekmesine enjeksiyon yok.

---

## §36.1 Agent Prompt — 4M-BE (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4M · 4M-BE — Hedefler sayıları: "bu hafta" = seçili hafta, quickCounts, plannedThisWeek (CRM 3D okumaları)
Repository: C:\tmp\vp-4m-be (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4m-be · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4M-targets-numbers-selected-week.md — 4M-BE (madde 1–2). Bağlam: WP-VP-3D §37 + CT ccd93de04, WP-VP-4L §37 (FrequencyDefaults, ?weekStart, WithExtraWeek). Kod: services/Diten.CrmService/src/**/Features/VisitPlanning/TargetStatus/** (ContactPeriodStatusReader.IsDueThisWeek, GetAccountDoctorsQuery, GetSessionTargetsQuery, TargetStatusPeriods) · Api/Controllers/CRM/VisitPlanningController.cs.
NE: (1) ?weekStart verildiğinde dueThisWeek o haftaya göre (IsDueThisWeek referansı = weekStart; yoksa bugün); quick=due süzgeci aynı referansla; yanıta quickCounts {due, never, all}; geçersiz/dönem dışı weekStart 400 invalid_week; haftalık varsayılan (4L) doktor kalan>0 ve aralık tutuyorsa due — "unknown ⇒ due değil" dalı yalnız required==null. (2) durum ek alanı plannedThisWeek (seçili haftada yazılmış planlı/onaylı ziyaret; parametresiz null).
KORU/YAPMA: yalnız okuma; yeni yazma komutu yok (27); 3A/3D/4L kuralları aynen; mobil yalnız ek alan; TenantId; göç/seed yok.
DOĞRULA (E2): CRM (2466/0/5 tabanı; bilinen kararsız testler olabilir) · Web 816/0 (dokunulmaz) · mimari 27. Testler 4M-BE Acceptance 1–6; sabotaj 1 (kırmızı kanıtla, geri al — `git checkout --` kullanma). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VP-4M-BE — target status by the selected week, quick filter counts, planned this week" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, mobil için yeni alanlar, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — 4M-WEB (paste-ready)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4M · 4M-WEB — Hedefler: yapılan/planlı/kalan, plandaki doktorlar başta, seçili hafta (Web)
Repository: C:\tmp\vp-4m-web (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4m-web · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4M-targets-numbers-selected-week.md — 4M-WEB (madde 3–5); sözleşme 4M-BE madde 1–2 (weekStart, quickCounts, plannedThisWeek). Bağlam: WP-VP-4H/4I/4J/4L §37 + CT eşit yükseklik düzeltmesi 7e562ce28. Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · Views/CRM/VisitPlanning/** · Resources/Views/CRM/VisitPlanning/**.
NE: (3) "Yapılan / kalan" hücresi: üst "yapılan / gereken", alt "N planlı · M kalan", gereken yoksa "—", ratio() RTL yalıtımı, ipucu. (4) seçili kurumun PLANDAKİ doktorları hızlı süzgeçten bağımsız tablonun başında ("Planda (N)" / "Diğer doktorlar" ara başlıkları); süzgeç ve sayılar yalnız diğerlerine; "Tümünü seç (N)" mevcut kural. (5) doktor ve plan hedefleri okumalarına weekStart = seçili hafta; hafta değişince yenile (önbellek anahtarı hafta dahil); sayılar quickCounts'tan (yoksa bugünkü yöntem); rozet metni "Bu hafta görülmeli" seçili haftaya göre. 4M-BE alanları yoksa bugünkü davranış, hata yok.
KORU/YAPMA: backend'e dokunma; yeni yazma ucu yok; mockup v3 Hedefler + eşit yükseklik + 4J/4L davranışları aynen; UAS-001; 7 dil + TR diakritik; argümansız Localizer'da {0} yok; RTL bidi/ratio/isolate; kullanıcının sekmesine enjeksiyon yok; canlı veriye yazma yok.
DOĞRULA (E2): Web (816/0 tabanı) · CRM 2466/0/5 (dokunulmaz) · mimari 27; JS node --check; -o çıktısı REPO İÇİNDE. Testler 4M-WEB Acceptance 1–5; sabotaj 1 (kırmızı kanıtla, geri al — `git checkout --` kullanma).
Commit: "feat(web): WP-VP-4M-WEB — done/planned/remaining, plan doctors first, status by the selected week" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, elle denenecekler. §22 TÜRKÇE. K13.
```
