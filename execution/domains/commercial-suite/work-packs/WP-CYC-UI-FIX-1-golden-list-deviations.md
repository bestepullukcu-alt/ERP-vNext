# WORK PACKAGE — WP-CYC-UI-FIX-1 · Dönemler + Dönem Kapasitesi listeleri: altın şablon sapmaları 1–3 (veri modu, `_TableSkeleton`, hızlı bakış) + 4–12 bilinen sapma kaydı

> **CT (SoR), 2026-10-05.**
> - **Kaynak:** WP-CYC-UI-1 §37 ve WP-CYC-UI-2 §37 — liste doğrulayıcısı iki listede de 12 sapma buldu (hepsi bu paketlerden önce de vardı).
> - **Kullanıcı (2026-10-05):** "evet 3 düzeltmeyi paketle" — 1–3 düzeltilir; 4–12 bilinen sapma olarak kaydedilir.
> - **Kapsam:** yalnız `frontend/Diten.Web` (iki liste). CRM DOKUNMA.
>
> **Çalışma yeri:** worktree `C:\tmp\cyc-ui-fix-1`, dal `wp/cyc-ui-fix-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kanıt (CT)
- **Kural:** `.antigravity/rules/frontend-datatable-template.md`; doğrulayıcı `.antigravity/scripts/verify_datatable_page.py` (iki listede de `--api-profile proxy` ile çalıştır — sayfalar aynı köken proxy kullanıyor).
- **Ortak parçalar:** `Views/Shared/_TableSkeleton.cshtml`; hızlı bakış (`#offcanvasDetailsPreview`) örnekleri `Views/CRM/{Claims, EligibilityPolicies, KnowledgeConcepts}/Index.cshtml` (+ ilgili JS).
- **Listeler:** `Views/CRM/CyclePeriods/{Index, _DataTable}.cshtml` + `wwwroot/assets/js/CRM/CyclePeriods/index.js` (tablo / zaman çizelgesi geçişi — WP-CYC-UI-1); `Views/CRM/CycleCapacities/{Index, _DataTable}.cshtml` + `wwwroot/assets/js/CRM/CycleCapacities/index.js` (WP-CYC-UI-2; satır başına tembel tahmin).

## NE
1. **Veri modu bildirimi:** iki listede şablonun beklediği biçimde veri modu (istemci / sunucu sayfalama — bugünkü gerçek davranış neyse) bildirilir.
2. **`_TableSkeleton`:** eski beş çubuklu yükleme bloğu kaldırılır, ortak `_TableSkeleton` kullanılır (zaman çizelgesi görünümünün kendi yükleme hâli değişmez).
3. **Hızlı bakış paneli (`#offcanvasDetailsPreview`):** satırda göz ikonu / satır eylemi → sağdan özet:
   - **Dönem:** kod, ad, tarihler + gün, kapsam, durum çizgisi, kapasite var mı, kampanya / planlanan ziyaret sayısı (liste satırındaki toplu alanlardan; ek istek yok), "Ayrıntıya git";
   - **Kapasite:** dönem, takvim ülkesi, tipik ziyaret süresi (+ eski model rozeti), tahmini ziyaret (satır için okunmuşsa; yoksa "hesaplanıyor" / "hesaplanamadı"), ort. FTE, ürün sınırları, "Ayrıntıya git".
   - Tüm değerler `textContent` / `esc()`; yetki aynı (okuma).
4. **Bilinen sapma kaydı (4–12):** iki WP'nin §37'sine ve sayfa denetleyicisinin belge yorumuna:
   - 4 paylaşılan `personalization-client.js` kiracı başlığı kontrolü (modül dışı ortak dosya — ayrı iş);
   - 5 doğrulayıcının doğrudan gateway profili beklentisi (sayfalar bilinçli aynı köken proxy);
   - 6–12 toplu seçim / toplu işlem / `/bulk` / toplu silme / `reloadWithToast` / seçimi temizle — modülde silme yok (arşiv), toplu yüzey yok.

## KORU / YAPMA
- Liste sütunları, filtreler, zaman çizelgesi, tembel tahmin, sağ panel form, ayrıntı sayfaları DEĞİŞMEZ.
- Yeni CRM çağrısı yok (hızlı bakış mevcut satır verisinden; kapasite tahmini zaten okunmuşsa kullanılır).
- Ortak dosyalar (`personalization-client.js`, `_TableSkeleton.cshtml`) DEĞİŞMEZ.

## Acceptance
- **E2:** Web 0 kırmızı (taban **536/0**), CRM 0 kırmızı (dokunulmadı; taban 2231/0/5), build 0 hata.
  - **Doğrulayıcı:** iki listede 1–3 artık **geçer**; kalan sapmalar yalnız 4–12 (sayı ve liste raporda).
  - **Yeni testler:** iki listede `_TableSkeleton` kullanımı + veri modu bildirimi; hızlı bakış paneli işaretlemesi ve alanları (XSS: `<script>` adlı dönem düz metin); hızlı bakış ek CRM isteği yapmıyor.
  - **Sabotaj:** hızlı bakışta bir alanı `innerHTML` ile yaz → XSS testi kırmızı; geri al.
- **E4:** CT canlı kontrolüyle birlikte (CYC-UI E4).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-CYC-UI-FIX-1 · Dönemler + Dönem Kapasitesi listeleri: altın şablon sapmaları 1–3 (veri modu, _TableSkeleton, hızlı bakış) + 4–12 bilinen sapma kaydı
Repository: C:\tmp\cyc-ui-fix-1 (worktree) · Branch: wp/cyc-ui-fix-1 · commit bu dala, push YOK · yalnız frontend/Diten.Web (iki liste)

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-CYC-UI-FIX-1-golden-list-deviations.md — önce tamamını oku. Ayrıca: …/WP-CYC-UI-1-cycle-periods-screens.md + …/WP-CYC-UI-2-cycle-capacity-screens.md (§37) · .antigravity/rules/frontend-datatable-template.md · .antigravity/scripts/verify_datatable_page.py · frontend/Diten.Web/Views/Shared/_TableSkeleton.cshtml · hızlı bakış örnekleri Views/CRM/{Claims, EligibilityPolicies, KnowledgeConcepts}/Index.cshtml (+ JS) · Views/CRM/{CyclePeriods, CycleCapacities}/** + wwwroot/assets/js/CRM/{CyclePeriods, CycleCapacities}/index.js.

NE: (1) iki listede veri modu bildirimi (bugünkü gerçek davranış). (2) eski yükleme bloğu yerine ortak _TableSkeleton (zaman çizelgesinin yükleme hâli aynı). (3) #offcanvasDetailsPreview hızlı bakış: Dönem (kod, ad, tarih+gün, kapsam, durum, kapasite var mı, kampanya/ziyaret sayısı — satır verisinden), Kapasite (dönem, takvim ülkesi, tipik ziyaret + eski model rozeti, tahmini ziyaret okunmuşsa, ort. FTE, ürün sınırları); "Ayrıntıya git"; textContent/esc(); ek CRM isteği YOK. (4) 4–12 bilinen sapma: iki WP §37'sine + denetleyici belge yorumuna kayıt.
KORU/YAPMA: sütunlar/filtreler/zaman çizelgesi/tembel tahmin/sağ panel/ayrıntılar DEĞİŞMEZ; yeni CRM çağrısı yok; ortak dosyalar (personalization-client.js, _TableSkeleton.cshtml) DEĞİŞMEZ; CRM DOKUNMA.
DOĞRULA (E2): dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 536); CRM testleri 0 kırmızı (taban 2231/0/5); build 0 hata; verify_datatable_page.py iki listede (--api-profile proxy) → 1–3 geçer, kalan yalnız 4–12 (raporla). Yeni testler WP Acceptance. Sabotaj: hızlı bakışta bir alanı innerHTML ile yaz → XSS testi kırmızı; geri al. Commit ("fix(web): WP-CYC-UI-FIX-1 — cycle periods + capacity lists: data mode, shared table skeleton, quick view; known deviations recorded" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
```

---

## §37 — CT bağımsız doğrulama (2026-10-06) — **ACCEPTED (E2)**
- **Commit:** ajan `030e562e` (taban `e01f1f13`) → `test/crm-content-visit-e2e` fast-forward. 27 dosya (+492 / −32). Yalnız Web + belge kayıtları.
- **K13:** iki liste `data-dt-data-mode="client"` (gerçek davranış), ortak `_TableSkeleton` (yükleme sonunda / hatada kaldırılıyor), `#offcanvasDetailsPreview` hızlı bakış satır verisinden (ek istek yok, `textContent`); 4–12 bilinen sapma iki WP §37'sine + denetleyici yorumlarına yazıldı; ortak dosyalar değişmedi.
- **CT testleri:** Web **551/0** (+15); CRM dokunulmadı (2231/0/5).
- **CT doğrulayıcı (`--api-profile proxy --format gaps`):** iki listede de yalnız 8 bilinen sapma (personalization-client + 7 toplu işlem maddesi); 1–3 geçiyor.
- **CT sabotajı:** Kapasite listesinden veri modu bildirimi kaldırıldı → 1 kırmızı. Geri alındı. Ajan: hızlı bakışta `innerHTML` → XSS testi kırmızı.
- **E4:** CYC-UI canlı kontrolüyle birlikte.
