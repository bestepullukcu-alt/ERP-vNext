# WORK PACKAGE — WP-KP-5a-UI · Güvenlilik Metinleri + Ülke Yasal Profilleri ekranları (Web) + menü kaydı

> **CT (SoR), 2026-10-01.**
> - **Eş paket:** WP-KP-5a (CRM). Sözleşme **WP-KP-5a §Sözleşme** tablosundadır; iki paket paralel çalışır, bu paket CRM'i proxy üzerinden çağırır (testlerde sahte gateway).
> - **Kullanıcı (2026-10-01):** güvenlilik metnini yalnız Regülasyon onaylar. Karar ekranda değil **iş akışında** (K1): ekrandaki "Onayla / Reddet" CRM karar ucunu çağırır, o da Platform görevini kapatır.
> - **Kapsam:** `frontend/Diten.Web` + Platform `CrmManifestProvider` (2 sayfa kaydı). CRM DOKUNMA.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-5a-ui`, dal `wp/kp-5a-ui`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Regülasyon ve içerik ekipleri:
- güvenlilik metinlerini (ürün × ülke × dil) ve ülke yasal profillerini (ülke × dil) listeleyip filtreleyebilsin;
- taslak oluşturup düzenleyebilsin, Regülasyon onayına gönderebilsin, geri çekebilsin;
- atanan Regülasyon onaycısı ayrıntı sayfasında **yorumla onaylayıp reddedebilsin** (WCN görevinin derin bağlantısı buraya gelir);
- sürüm geçmişini ve karar kaydını görebilsin; aktif sürümden yeni sürüm açabilsin.

## Kanıt (CT)
- **Altın liste deseni:** `Views/CRM/Claims/**` + `wwwroot/assets/js/CRM/Claims/index.js` (İddialar v2; 15 bilinen sapma var — **kopyalama, altın şablonu izle**); Bilgi Yolları listesi (`Views/CRM/KnowledgePaths/**`, KP-UI-1; 12 bilinen sapma kaydı).
- **İnceleyici görünümü deseni:** KP-UI-2 (`WP-KP-UI-2-path-mlr-release-screens.md`): karar paneli, ret yorumu zorunlu, `canDecide` ile görünürlük, derin bağlantı rotası zorunlu.
- **Menü:** Platform `services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs` — `KNOWLEDGE_PATHS` (sıra 120), `CLAIMS` (140) `ModuleManifestPage` + `ModuleManifestAction` deseni. Görünürlük zinciri: memory `module-nav-visibility-chain`; nav L10n guard'ı (değer ≠ anahtar).
- **Seçiciler:** ürün = MDM global product seçicisi (strateji şablonu `api/global-products` deseni); ülke + dil = Bilgi Yolu kimlik seçicisi (KP-UI-1; `country-content-languages`).
- **UAS-001:** yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz.
- **L10n:** tenant modülü → 7 dil (en, tr, fr, es, zh, ar, ru), TR diakritik, RTL (ar).

## NE
1. **Güvenlilik Metinleri** (`/CRM/SafetyTexts`):
   - **liste (altın şablon):** kod, ürün, ülke, dil, sürüm, durum rozeti (taslak / incelemede / aktif / yerini aldı / arşiv), güncelleme; filtreler ürün / ülke / dil / durum; arşivliler gizli (anahtar); "yalnız aktif" hızlı filtresi; toplu silme YOK (arşiv).
   - **oluştur / düzenle (yalnız taslak):** ürün, ülke, dil (kimlik; oluşturduktan sonra değişmez), Body (çok satırlı, karakter sayacı ≤ 20 000), ShortBody (≤ 2 000), kaynak belge, kaynak tarihi, yerel onay numarası.
   - **ayrıntı (`/CRM/SafetyTexts/{id}`, WCN derin bağlantısı):** metin önizlemesi (paragraflar korunur, HTML olarak YORUMLANMAZ); durum çizgisi; sürüm geçmişi (aynı anahtar); karar kaydı (kim, sonuç, yorum, ne zaman); eylemler durum + izinle: Düzenle, Onaya gönder, Geri çek, Yeni sürüm, Arşivle.
   - **karar paneli:** yalnız `canDecide` iken; Onayla (yorum isteğe bağlı) / Reddet (yorum zorunlu, istemci de engeller); sonuçtan sonra sayfa durumu tazelenir (onay → aktif). Gönderen kendi kaydında paneli görmez.
2. **Ülke Yasal Profilleri** (`/CRM/LegalProfiles`): aynı yapı; kimlik ülke + dil; alanlar yasal altbilgi (zorunlu), ruhsat sahibi, yan etki bildirim metni, tanıtım notu, sayfa onay kodu biçimi (örnek önizlemesiyle: `TR-2026-0001`).
3. **Hata gösterimi:** WP-KP-5a hata kodları alan altında / form başında yerelleştirilmiş metinle; `review_template_missing` → "Bu ülke için Regülasyon onay şablonu tanımlı değil" + yöneticiye not; `dependency_unavailable` → seçici devre dışı + açıklama.
4. **Menü:** `CrmManifestProvider`'a iki sayfa — `SAFETY_TEXTS` ("Safety Texts", `/CRM/SafetyTexts`, `crm.safety-text.read`, sıra 150) ve `LEGAL_PROFILES` ("Legal Profiles", `/CRM/LegalProfiles`, `crm.country-legal-profile.read`, sıra 160); eylemler `MANAGE` (Toolbar, `…manage`), `SUBMIT` (RowAction, `…submit`). Nav L10n 7 dil (Platform / Web nav kaynakları — mevcut desen).
5. **L10n:** tüm metinler 7 dil, TR diakritik; `_IndexL10n` dizileri + PascalCase köprü; RTL'de form ve önizleme mantıksal CSS.

## KORU / YAPMA
- **CRM DOKUNMA** (sözleşme WP-KP-5a'da). Auth DOKUNMA.
- Kullanıcı metni DOM'a yalnız `textContent` / `esc()` ile (güvenlilik metni HTML değildir).
- Proxy 204 tuzağı (memory `proxy-forward-204-content-length-crash`); gateway 404 + `{}` = rota yok (memory `gateway-404-empty-body-signature`) → CRM rotası `/api/crm/knowledge/*` altında mevcut ocelot rotasıyla gider; yeni rota gerekirse raporla.
- İddialar / Bilgi Yolları ekranları DEĞİŞMEZ.
- **DUR:** manifest kaydı Platform'da başka bir tüketiciyi (plan yetkileri, entitlement) kırıyorsa → raporla.

## Acceptance
- **E2:** Web 0 kırmızı (taban **422/0**; KP-CH-1 birleşirse artar), Platform testleri 0 kırmızı (manifest), CRM 0 kırmızı (dokunulmadığını doğrula), build 0 hata.
  - **Yeni testler:** proxy uçları (liste / detay / oluştur / güncelle / gönder / geri çek / karar / yeni sürüm / arşiv / çözümleme) doğru CRM yoluna ve gövdeyle; ret yorumu istemci + sunucu; `canDecide` false iken karar paneli yok; yetkisizde sayfa iskeleti yok (UAS-001); manifest iki sayfa + eylemler; nav L10n değer ≠ anahtar; resx 7 dil eşit anahtar; metin önizlemesi HTML yorumlamıyor (`<script>` metni düz görünür).
  - **Altın liste kontrolü:** iki yeni liste ekranı golden şablon denetiminden **sapmasız** geçmeli (yeni ekranlarda bilinen sapma kabul edilmez).
  - **Sabotaj:** (1) ret yorumu istemci kontrolü kaldırılınca test kırmızı; (2) önizlemede `textContent` yerine `innerHTML` → XSS testi kırmızı.
- **E4 (CT, KP-5a + CFG sonrası):** menüde iki sayfa; TR / tr güvenlilik metni oluştur → gönder → sema WCN görevinden derin bağlantıyla gelir → onay → aktif.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-KP-5a-UI · Güvenlilik Metinleri + Ülke Yasal Profilleri ekranları (Web) + menü kaydı
Repository: C:\tmp\kp-5a-ui (worktree) · Branch: wp/kp-5a-ui · commit bu dala, push YOK · frontend/Diten.Web + Platform CrmManifestProvider (2 sayfa)

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-KP-5a-UI-safety-text-legal-profile-screens.md — önce tamamını oku. Sözleşme: …/WP-KP-5a-safety-text-legal-profile.md §Sözleşme (CRM paralel yazılıyor; testlerde sahte gateway). Ayrıca: …/WP-KP-UI-2-path-mlr-release-screens.md (karar paneli deseni) · …/WP-KP-UI-1-*.md (liste + ülke/dil seçici) · frontend/Diten.Web/Views/CRM/{Claims,KnowledgePaths}/** + wwwroot/assets/js/CRM/{Claims,KnowledgePaths}/** · Controllers/CRM/StrategyTemplatesController.cs (api/global-products seçici) · services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs · .antigravity/rules altın liste şablonu · memory module-nav-visibility-chain, l10n-bridge-pascalcase-loader, proxy-forward-204-content-length-crash, gateway-404-empty-body-signature, dt-inline-filter-host-class, updatevisualstate-global-selectors.

NE: (1) /CRM/SafetyTexts: altın şablon liste (kod, ürün, ülke, dil, sürüm, durum rozeti; filtreler ürün/ülke/dil/durum; arşiv anahtarı; "yalnız aktif"; toplu silme YOK); oluştur/düzenle yalnız taslak (ürün, ülke, dil kimlik — sonra değişmez; Body ≤20000 sayaçlı; ShortBody ≤2000; kaynak belge/tarih; yerel onay no); ayrıntı /CRM/SafetyTexts/{id} (WCN derin bağlantısı): textContent önizleme, durum çizgisi, sürüm geçmişi, karar kaydı, eylemler (Düzenle, Onaya gönder, Geri çek, Yeni sürüm, Arşivle); karar paneli yalnız canDecide: Onayla (yorum ops.) / Reddet (yorum zorunlu, istemci de engeller). (2) /CRM/LegalProfiles aynı yapı; kimlik ülke+dil; alanlar yasal altbilgi (zorunlu), ruhsat sahibi, yan etki bildirim, tanıtım notu, sayfa onay kodu biçimi (örnek önizleme). (3) KP-5a hata kodları yerelleştirilmiş, alan altında/form başında. (4) CrmManifestProvider: SAFETY_TEXTS (/CRM/SafetyTexts, crm.safety-text.read, 150) + LEGAL_PROFILES (/CRM/LegalProfiles, crm.country-legal-profile.read, 160); eylemler MANAGE (Toolbar) + SUBMIT (RowAction); nav L10n 7 dil. (5) L10n 7 dil, TR diakritik, PascalCase köprü, RTL mantıksal CSS.
KORU/YAPMA: CRM ve Auth DOKUNMA; textContent/esc(); UAS-001 (yetkisize iskelet yok, yönlendirme yok); İddialar/Bilgi Yolları ekranları DEĞİŞMEZ; yeni gateway rotası gerekirse raporla.
DOĞRULA (E2): cd C:\tmp\kp-5a-ui; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 422); Platform testleri (manifest) 0 kırmızı; CRM testleri 0 kırmızı (dokunulmadı); build 0 hata. Yeni testler WP Acceptance listesi; iki yeni liste altın şablon denetiminden SAPMASIZ. Sabotaj: (1) ret yorumu istemci kontrolü kaldır → kırmızı; (2) önizlemede innerHTML → XSS testi kırmızı. Commit ("feat(web): WP-KP-5a-UI — safety texts + country legal profiles screens, regulatory decision panel, CRM manifest pages" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: manifest kaydı plan yetkileri/entitlement tüketicisini kırıyorsa ya da yeni gateway rotası gerekiyorsa → DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-02) — **ACCEPTED (E2, sahte gateway)**
- **Commit:** ajan `bfaa1902` (taban `47d7ffd0`) → `test/crm-content-visit-e2e` fast-forward. 53 dosya (+4392). Web + Platform `CrmManifestProvider`; CRM diff yok.
- **Diff (K13 okuma):** ortak `RegulatoryTextsControllerBase` (proxy, izinler, 204 gövdesiz, ret yorumu sunucuda da zorunlu — gateway'e gitmeden 400); `details.js` tüm `innerHTML` şablonları `esc()`'li, önizleme `textContent`; Compact yapı; menü `SAFETY_TEXTS` 150 / `LEGAL_PROFILES` 160 (MANAGE + SUBMIT); 82 + 84 anahtar × 7 dil.
- **CT testleri:** Web **459/0** (+37), Platform manifest testleri **6/0**. Ajan: CRM 2147/0/5 (dokunulmadı), Platform 174 kırmızı = 173 ortam tabanı + 1 Mongo oynaklığı (tek başına yeşil).
- **CT sabotajı:** sunucu tarafı yorumsuz ret kontrolü kapatıldı → 2 kırmızı. Kod geri alındı. Ajan: istemci ret yorumu + `innerHTML` XSS.
- **Kabul edilen kararlar:** Compact (8 alan sınırı; derin bağlantı + 20 000 karakter + karar paneli); aynı köken proxy profili (İddialar / Bilgi Yolları gibi); paylaşılan `personalization-client.js` maddesi bütün listelerde → ayrı iş.
- **Açık (KP-5a birleşince CT):** sözleşmede kesin JSON adları yoktu — Web `id` / `safetyTextId` / `countryLegalProfileId` / `legalProfileId` ve oluşturma yanıtı için birden çok ad kabul ediyor, CRM listesinin sayfasız döndüğünü varsayıyor → gerçek KP-5a DTO'larıyla karşılaştırılacak; uyumsuzluk → küçük FIX.
- **Kullanıcı (2026-10-02):** mockup sorulmadan paketlendi; "devam etsin, sonradan bakarız, olmadıysa mockup yaptırırız".
- **E4:** KP-5a + KP-5a-CFG sonrası.
