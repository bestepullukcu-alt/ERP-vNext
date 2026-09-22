# WORK PACKAGE — WP-ST-DETAIL-4 · Detay E4 rötuş: 8 mockup farkı (uyarı temizliği + ürün/içerik/frekans/sürüm-geçmişi mockup + kart taşıma) (frontend)

> **CT (SoR).** MOD-0167-FU04, Faz 3 Detay E4 rötuş. Branch `feature/crm-scmm-studio` (`71f62414` üstü — WP-ST-DETAIL-3 §37 sonrası). **Kullanıcı E4 (mockup yan yana) 8 fark.** **Frontend** (Details.cshtml + details.js + strategy-create.css? + resx/L köprüsü gerekiyorsa). Backend/CrmService DEĞİŞMEZ.

## NE (8 madde; frontend; backend DEĞİŞMEZ)
1. **A — SKU uyarısını KALDIR:** Details.cshtml:251-252 `ContainmentNotVerifiedHelp` alert ("Bir SKU'nun seçilen ürüne ait olup olmadığı burada doğrulanmaz…") → **sil** (authoring uyarısı, salt-okunur Detay'da yeri yok; mockup'ta yok).
2. **B — Segment info kutusunu KALDIR:** Details.cshtml:155-156 `NoMembersEverHelp` alert ("Plan, segmentin kimliğini bağlar. İçindeki kişileri asla listelemez veya saymaz.") → **sil** (mockup segment bölümünde yok).
3. **C — İçerik kartları 2/satır:** İçerik bölümü (315+) kartları şu an alt alta; mockup **bir satırda 2** → `row g-3` + her kart `col-12 col-md-6`. Kart içeriği (ad + kind·kod + "sabitlenmiş") KORUNUR.
4. **D — Ürün + SKU kartı mockup'a:** ürün satırı (279-281) `badge LineWeightPercentage: %` KALDIR → mockup düzeni: **sol** = renkli kare + ürün **adı** (data-resolve) + **alt-satır** "MDM-GP kod · SKU kod" (SKU = satırın ilk skuAllocation gsku kodu; yoksa "SKU belirtilmedi"); **sağ** = **% düz** (badge değil, sağ hizalı, büyükçe). Σ progress bar KORUNUR.
5. **E — Frekans renkli info kutusu:** FREKANS bölümü (200-210) düz dl + `badge Mode` → mockup **renkli info kutusu** (Düzenle `.st-freq-info` / alert-primary-subtle stili): policy-reference'ta "● Politikaya işaretçi: {policy kod} · {ad}" + gri açıklama ("Gerçek ziyaret sıklığı Ziyaret Sıklığı Politikaları modülünde hedef/segment bazında yönetilir — bu oyun yalnızca referans tutar.") + "Ziyaret Sıklığı Politikaları'nı aç ↗" link. declared-intent/none modlarında uygun kısa metin. Policy adı data-resolve korunur.
6. **F — Sürüm geçmişi timeline (details.js):** version render (87-90) düz satır → mockup **timeline**: her sürüm **sol nokta** + "vN · {durum}" + **alt-satır**: geçerli/aktif ise "Son güncelleme {tarih}", donmuş sürümlerde "Bağları değişmez; yalnızca görüntülenebilir". newest-first korunur. (Küçük css: sol-nokta timeline; strategy-create.css veya inline.)
7. **G — Sınıflandırma + Yaşam döngüsü kartlarını KALDIR:** sağ sütundaki SINIFLANDIRMA (Özne tipi/İş birimi) + YAŞAM DÖNGÜSÜ (Durum/Bağlar/Geçerlilik…) kartları mockup'ta YOK → **sil** (metadata Detay'ın diğer yerlerinde/summary'de zaten var; EffectiveFrom/To gerekiyorsa Kimlik/summary'ye taşımadan sadece kaldır — mockup göstermiyor).
8. **H — Header badge'lerini KİMLİK kartına taşı:** page header'daki (21-22) Taslak/vN badge'leri → **KİMLİK kartının sağ üstüne** (Düzenle Kimlik kartı deseni: `d-flex justify-content-between` + sağda durum + `v(N)` badge). Header'da yalnız ad + breadcrumb + aksiyonlar kalır.
9. **(minör) KAPSAM etiketleri:** "TÜZEL KİŞİ" → **"TÜZEL KİŞİLİK"**, "İŞ BİRİMİ" → **"İŞ BIRIMI / TERRITORY"** (mockup). Tenant-scoped play'de "—" doğru (veri yok) — data-resolve korunur.

## KORU / YAPMA
- Backend/CrmService/DETAIL-1/gateway DEĞİŞMEZ. İsim çözümü (data-resolve feed'leri, DETAIL-3) + Σ progress + stat tile + KAPSAM 3 mini-kart + human summary + header aksiyonları (Düzenle/Aktifleştir/NewVersion/Arşivle) + Kampanyada kullan placeholder KORUNUR. Çözülemeyen ref → kod/ham fallback (uydurma yok). Liste/Edit/Create/form.js/_Form/_SidePanel DOKUNMA. Tema/L10n köprüsü.
- **DUR:** Yaşam döngüsü kartı kaldırılınca EffectiveFrom/To'ya bağlı başka bir gösterim kırılıyorsa → DUR+raporla (mockup göstermiyor, kaldırmak yeterli). Header badge taşıması bir aksiyonu/id'yi kırıyorsa → DUR.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: Details.cshtml + details.js (+ css/resx gerekirse). **CrmService/backend/gateway/Edit/Create/form.js/_Form/_SidePanel diff YOK.**
- **E4 (FLEET RESTART):** SKU/segment uyarıları yok; içerik 2/satır; ürün satırı ad + (MDM-GP · SKU) alt + % sağda; frekans renkli info kutusu; sürüm geçmişi sol-noktalı timeline + alt metin; Sınıflandırma/Yaşam döngüsü kartları yok; Taslak/vN Kimlik kartının sağ üstünde. **Razor+js(+css/resx) → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-DETAIL-4 · Detay E4 rötuş — 8 mockup farkı (uyarı temizliği + ürün/içerik/frekans/sürüm-geçmişi mockup + kart taşıma) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 71f62414 üstü · Worktree: ana checkout

Kullanıcı E4 (mockup yan yana) 8 fark. Backend/CrmService DEĞİŞMEZ; DETAIL-3 isim çözümü/stat/KAPSAM/summary KORUNUR.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-DETAIL-4-detail-e4-refine.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/Details.cshtml (SKU uyarı 251, segment info 155, header badge 21-22, frekans 200-210, ürün 279-281, içerik 315+, sağ sütun Sınıflandırma/Yaşam döngüsü kartları) · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/details.js (version render 87-90) · REFERANS _Form.cshtml (Kimlik kartı sağ-üst badge deseni + .st-freq-info frekans kutusu) · strategy-create.css (.st-freq-info) · resx.

NE (8; frontend; backend DEĞİŞMEZ):
 1) SKU uyarısı (251 ContainmentNotVerifiedHelp alert) SİL.
 2) Segment info kutusu (155 NoMembersEverHelp alert) SİL.
 3) İçerik kartları 2/satır: row g-3 + col-12 col-md-6 (ad+kind·kod+sabitlenmiş korunur).
 4) Ürün satırı: badge "LineWeightPercentage %" KALDIR → sol renkli kare + ürün adı(data-resolve) + alt "MDM-GP kod · SKU kod"(ilk skuAllocation gsku kodu; yoksa "SKU belirtilmedi"); sağ % düz sağ-hizalı. Σ progress KORU.
 5) Frekans: düz dl+badge → renkli info kutusu (.st-freq-info / alert-primary-subtle): "● Politikaya işaretçi: {kod} · {ad}" + gri açıklama + "Ziyaret Sıklığı Politikaları'nı aç ↗"; declared-intent/none uygun metin; policy data-resolve korunur.
 6) details.js version render (87-90) → timeline: sol nokta + "vN · {durum}" + alt: geçerli/aktif "Son güncelleme {tarih}", donmuş "Bağları değişmez; yalnızca görüntülenebilir". newest-first korunur. gerekli küçük css.
 7) Sağ sütun SINIFLANDIRMA + YAŞAM DÖNGÜSÜ kartlarını SİL (mockup'ta yok).
 8) Header (21-22) Taslak/vN badge'lerini KİMLİK kartı sağ-üstüne taşı (Düzenle deseni d-flex justify-content-between). Header'da ad+breadcrumb+aksiyon kalır.
 9) KAPSAM etiketleri: TÜZEL KİŞİ→TÜZEL KİŞİLİK, İŞ BİRİMİ→İŞ BIRIMI / TERRITORY.
KORU/YAPMA: backend/CrmService/DETAIL-1/gateway DEĞİŞMEZ; DETAIL-3 isim çözümü(data-resolve)+Σ progress+stat tile+KAPSAM+human summary+header aksiyonları+Kampanyada kullan KORU; çözülemeyen→kod/ham fallback (uydurma yok); Liste/Edit/Create/form.js/_Form/_SidePanel DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff Details.cshtml+details.js(+css/resx); CrmService/backend/gateway/Edit/Create/form.js/_Form/_SidePanel diff yok. Ayrı commit ("feat(strategy): WP-ST-DETAIL-4 — Detay E4 rötuş (uyarı temizliği + ürün/içerik/frekans/sürüm-geçmişi mockup + badge taşıma) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: Yaşam döngüsü kaldırınca EffectiveFrom/To başka gösterimi kırıyorsa; header badge taşıması aksiyon/id kırıyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 0c0b99df · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stdetail4-verify @0c0b99df
```
- ✅ **Kapsam (10 dosya, +136/−137):** Details.cshtml + details.js + _IndexL10n + 7 resx. **CrmService/backend/gateway/Edit/Create/form.js/_Form/_SidePanel/Controller/css TEMİZ** ✓.
- ✅ **9 madde:** SKU uyarısı (ContainmentNotVerifiedHelp) + segment info (NoMembersEverHelp) SİLİNDİ (grep 0); içerik 2/satır (col-md-6); ürün satırı renkli kare + ad + MDM-GP·SKU alt + % sağda (LineWeight badge kalktı, Σ progress korundu); frekans `alert-primary` renkli info kutusu; details.js sürüm geçmişi timeline; SINIFLANDIRMA+YAŞAM DÖNGÜSÜ kartları silindi; header Taslak/vN → Kimlik kartı sağ-üstü; KAPSAM etiketleri Detay'a özel (TÜZEL KİŞİLİK / İŞ BIRIMI-TERRITORY).
- ✅ **KORU=0:** paylaşımlı `LegalEntity`/`BusinessUnit` resx anahtarları DEĞİŞMEDİ (Edit etkilenmez — Detay'a özel yeni anahtar); DETAIL-3 isim çözümü (data-resolve) + Σ progress + stat tile + KAPSAM mini-kart + human summary + header aksiyonları (Düzenle/Aktifleştir/NewVersion/Arşivle data-action) + Kampanyada kullan korundu. Yaşam döngüsü kaldırma EffectiveFrom/To'ya bağlı başka gösterimi kırmadı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: mockup yan yana — 8 fark giderildi. **FLEET RESTART.**

**WP-ST-DETAIL-4 KOMPLE (E2). Detay E4 rötuşları tamam; Faz 3 Detay mockup-tam. Sonraki: adım 3 content chain (F1 CEJ RBAC grant → canlı test) — kullanıcı onayı bekliyor.**
