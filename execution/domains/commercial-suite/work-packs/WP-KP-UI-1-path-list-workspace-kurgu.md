# WORK PACKAGE — WP-KP-UI-1 · Bilgi Yolu Stüdyosu — liste + çalışma alanı kabuğu + kurgu (frontend)

> **CT (SoR).**
> - **Tasarım:** `DESIGN-KP-STUDIO-knowledge-path-studio.md` §3.1, §6.
> - **Mockup (v2 kabul):** `mockups/kp-studio/kp-studio-prototype-v2.html` (çözümü `kp-studio-screens-v2.decoded.html`, metinler `kp-studio-screen-texts.txt`).
> - **Mockup kalanları** (`KP-STUDIO-mockup-analysis.md` v2 tablosu) bu pakette kapanır: **#1** yetkisiz kullanıcıya liste yok (UAS-001), **#4** ham kod yok (ülke / dil / içerik tipi adları), **#8** kurgu kartında iddia metni, filtrelerde TM / tk / be.
> - **Bağımlılık:** KP-1 birleşti (`95c2444d`).
> - **Paralel:** KP-2 (CRM). Bu paket **yalnız Web** (`frontend/Diten.Web`).
>
> **Çalışma yeri:** worktree `C:\tmp\kp-ui-1`, dal `wp/kp-ui-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kapsam
1. **Liste**
2. **Yeni yol** (zincir + ülke + dil)
3. **Çalışma alanı kabuğu:**
   - tek durum çizgisi;
   - bağlam şeridi;
   - **yalnız "Kurgu" sekmesi aktif**; diğer sekmeler KP-UI-2 / 3'te eklenir, bu pakette **gösterilmez**.
4. **Kurgu:**
   - dal sütunları, zincirden gelen adımlar;
   - adıma içerik / iddia ekleme, sıralama, çıkarma;
   - adım ayarları;
   - uyum özeti;
   - iddia arama.

**Bu pakette YOK:**
- MLR / revizyon / çıktı / yayın / önizleme / kullanım sekmeleri;
- eski yol sihirbazı (KP-UI-2);
- sayfa tasarımcısı (KP-UI-3).

## Kanıt (CT)
- **Mevcut Web:** `Controllers/CRM/KnowledgePathsController.cs` (MVC Create / Edit / Details + `api/*` proxy: paths, steps, subjects, topics, audience-profiles, contents, concept-nodes). `Views/CRM/KnowledgePaths/*`, `wwwroot/assets/js/CRM/KnowledgePaths/{index, form, details}.js`, resx `KnowledgePathsIndex`.
- **İzinler:** `crm.knowledge.path.read | manage | publish` (fallback `crm.territory.*`).
- **CRM (KP-1) uçları:**
  - `api/crm/knowledge/paths` (liste: `countryCode`, `languageCode`, `chainTemplateCode`, `isLegacyUnapproved`);
  - `paths/{id}` (detay: `chainTemplate`, `derivedContext`, `identityLocked`, `claims[]` {text, qualifier, countryVersion, usable, reason}, `chainConformance[]`);
  - `POST paths` (chainTemplateId + countryCode + languageCode);
  - `POST paths/{id}/bind-chain`;
  - `steps` (`arrangement` zorunlu, zincirli yolda);
  - `POST paths/{id}/claims`, `…/claims/{claimId}/arrange`, `…/claims/{claimId}/remove`.
  - **Hata kodları:** `chain_template_invalid`, `chain_template_required`, `chain_subject_mismatch`, `path_identity_locked`, `country_invalid`, `language_not_in_country`, `reference_set_unavailable`, `chain_slot_invalid`, `chain_slot_full`, `chain_slot_move_forbidden`, `component_language_mismatch`, `claim_product_mismatch`, `claim_ref_duplicate`.
- **Yeniden kullanılacak Web parçaları (kopyalama değil):**
  - ülke / dil görünen adları: `Controllers/CRM/ClaimDisplayNames.cs` + FE-2 `lookups/countries` deseni (`ClaimsController.V2.cs`);
  - iddia seçenekleri: FE-5 `KnowledgeController.Claims.cs` `claim-options` (coverage + ülke dilleri);
  - zincir şablon listesi: `KnowledgeConceptsController` (`/CRM/KnowledgeConcepts/Templates`).
- **Gotcha'lar:** memory `l10n-bridge-pascalcase-loader`, `dt-inline-filter-host-class`, `updatevisualstate-global-selectors`, `knowledge-taxonomy-sortorder-ux` (SortableJS'i DataTable tbody'ye bağlama).

## NE
1. **Proxy / lookup** (`KnowledgePathsController`, yeni partial):
   - KP-1 uçları için izinli proxy: `bind-chain`, `claims` add / arrange / remove.
   - **Lookup'lar:**
     - `lookups/chain-templates`: yalnız yayında + dallı olanlar; ad, sürüm, konu, ürün;
     - `lookups/countries`: ICU adı + dilleri, `ClaimDisplayNames` ile;
     - `lookups/path-contents?pathId`: yolun dilinde + ürün / konu uyumlu yayındaki içerikler, tip **etiketiyle**;
     - `lookups/path-claims?pathId&q`: ürün uyumlu iddialar; her biri için yolun ülkesinde sürüm / durum / kullanılabilirlik + gerekçe.
   - İddia seçeneklerinde FE-5 mantığı ortak yardımcıya alınır, kopyalanmaz.
2. **Liste** (`/CRM/KnowledgePaths`, mockup "Liste"):
   - **UAS-001:** `crm.knowledge.path.read` yoksa düz 403, iskelet yok (mockup kalanı #1).
   - **Sütunlar:** Yol (kod + ad), Ürün, Ülke / dil (**adlarıyla**), Zincir şablonu, Sürüm, Durum, "Onaysız eski yol" rozeti. Son revizyon ve yolculuk sayısı KP-UI-2'de.
   - **Filtreler:** ürün, ülke (6 ülke), dil (tr / ru / be / uz / tk / ka / az, yerel adıyla), durum, arama, "Yalnız onaysız eski yollar".
   - **Özet kartları** (tıklayınca filtre): toplam, taslak, yayında, onaysız eski.
   - **Eski yol satırı:** "Zincire bağla" düğmesi. Sihirbaz KP-UI-2'de; bu pakette düğme basit **bağlama modalını** açar (zincir + ülke + dil → `bind-chain`, hata kodları kullanıcı dilinde).
3. **Yeni yol:**
   - **zincir** (yalnız yayında + dallı);
   - **ülke** (ad);
   - **dil** (ülkenin dilleri, yerel ad);
   - ad, açıklama, amaç, geçerlilik.
   - Konu / ürün / kitle zincirden salt okunur önizleme. Kod boşsa sunucu `KP-…` üretir.
   - Kaydedince çalışma alanı açılır.
4. **Çalışma alanı kabuğu** (`/CRM/KnowledgePaths/{id}` → mevcut `Edit` / `Details`'in yerini alır):
   - **Başlık:** ad, `KP-…`, sürüm; **tek durum çizgisi** (taslak / incelemede / onaylı / yayında + "Sahada vX" varsa aynı satırda).
   - **Bağlam şeridi:** Zincir (ad + sürüm, kilit), Ülke (ad, kilit ikonu, "Yolun kimliği; başka ülke için Uyarla" ipucu), Dil (yerel ad, kilit), Ürün, Kitle (salt okunur).
   - **Sekme:** yalnız "Kurgu" (diğerleri sonraki paketlerde gelecek; boş sekme gösterme).
   - "Sıradaki adım" bandı ve tanıtım metni **ilk kullanımda** görünür, kapatılabilir (localStorage).
   - Eski yolda üstte "Onaysız eski yol — sahada kullanılamaz · Zincire bağla" bandı.
5. **Kurgu** (mockup v2 "Kurgu"):
   - **Dal sütunları:** her dal bir sütun, adımlar zincirden. **Dal / adım ekleme, çıkarma, taşıma YOK**; not: "Dal ve adımlar zincirden gelir".
   - **Adım kartı:** ad, zorunlu / isteğe bağlı, süre, ön koşul; **en az / en çok öğe ve durum** (uyum özetinden: ok / eksik / fazla); öğeler.
   - **İçerik öğesi:** başlık, **tip etiketi** (ham kod değil), dil **adı**, yayın durumu.
   - **İddia öğesi:** kod · ad · **metin** (yolun dilindeki ülke sürümü metni, mockup kalanı #8) · niteleyici · ülke sürümü + durum; kullanılamazsa kırmızı gerekçe (`not_approved` / `no_country_version` / `language_mismatch` → kullanıcı dilinde).
   - **Sıralama:** öğeler yalnız **kendi adımı içinde** sürüklenerek sıralanır. Ayrı bir sıralama listesi kullan; DataTable tbody değil. Klavye alternatifi: yukarı / aşağı düğmeleri.
   - **"Adıma ekle" paneli** (İçerik / Onaylı iddia sekmeleri): arama; her satırda "Ekle" ya da **gerekçeli "Eklenemez"** (dil farklı, ürün farklı, yayında değil, slot dolu). **İddia arama** (Öneri 1): kod / metin ile, yalnız yolun ürünü; ülke sürümü durumu gösterilir.
   - **Adım ayarları** (dişli): zorunlu, süre, ön koşul adımı. Slot min / max zincirden, düzenlenemez.
   - **Üstte "Sahadaki sıra":** dal-öncelikli hesaplanmış sıra (sunucu `StepOrder`).
   - Taslak dışı durumda salt okunur.
6. **Hatalar:** tüm CRM hata kodları **kullanıcı dilinde** metin olarak gösterilir. Kod ham gösterilmez; gerekirse ipucunda. Mockup v2 notu.
7. **L10n ve erişilebilirlik:**
   - yeni resx ailesi `KnowledgePathStudio.{en, tr, fr, es, zh, ar, ru}` + köprü (PascalCase);
   - ar için RTL (mantıksal CSS: `margin-inline-*`, `inset-inline-*`);
   - açık / koyu tema (mevcut tema değişkenleri, sabit hex yok);
   - klavye ile kullanılabilir, `aria-label`'lar.
8. **Eski ekranlar:** mevcut MVC `Create` / `Edit` / `Details` görünümleri yeni ekranlara yönlenir. Mevcut `api/*` proxy'leri korunur (yolculuk formu vb. kullanıyor olabilir, kontrol et).

## KORU / YAPMA
- **CRM / Platform / Auth DOKUNMA** (KP-2 paralel).
- **İddialar, Bilgi İçerikleri ve yolculuk ekranları DEĞİŞMEZ.** Ortaklaştırılan yardımcı dışında.
- Yayın düğmesi bu pakette **YOK** (zincirli yolda KP-2 409 verecek; yayın KP-UI-2).
- Eski yolun mevcut adım düzenleme davranışı korunur (zincirsiz yolda eski form).
- Tarayıcıda ayrı sekme, canlı yazma YOK.
- **DUR:** mevcut `api/*` proxy'lerinden biri başka bir ekranda kullanılıyor ve değişiklik onu bozacaksa → dur, raporla.

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban 343). Build 0 hata. `node --check` temiz.
  - **Yeni testler:**
    - liste / çalışma alanı izin kapısı (UAS-001 düz 403);
    - lookup'lar (yalnız yayında + dallı zincir, ülke / dil adları, içerik dil / ürün filtresi, iddia kullanılabilirlik gerekçesi);
    - proxy allowlist (yalnız izinli uçlar);
    - hata kodu → kullanıcı metni eşlemesi (13 kod);
    - L10n 7 dil + JS anahtar ↔ resx eşliği;
    - ham kod görünmemesi (ülke / dil / tip etiketi);
    - FE-5 claim-options davranışı değişmedi.
  - **Sabotaj:** izin kapısı ve iddia kullanılabilirlik gerekçesi testleri kırmızıya dönmeli.
- **E4 (CT):** fleet restart sonrası.
  - Liste ve eski yol rozetleri; yeni ALMIBA yolu (TR / tr); kurgu.
  - İngilizce içeriğin "Eklenemez" gerekçesi.
  - İddia kartında metin; slot doluluğu.
  - Yetkisiz kullanıcıya 403.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-KP-UI-1 · Bilgi Yolu Stüdyosu — liste + çalışma alanı kabuğu + kurgu (frontend)
Repository: C:\tmp\kp-ui-1 (worktree) · Branch: wp/kp-ui-1 · commit bu dala, push YOK · PARALEL: KP-2 (CRM) — sen yalnız frontend/Diten.Web

Amaç: Mockup v2 (Bilgi Yolu Stüdyosu) liste + yeni yol + çalışma alanı kabuğu + Kurgu sekmesi, KP-1 uçları üzerine. Diğer sekmeler (MLR/revizyon/çıktı/yayın/önizleme/kullanım) ve eski yol sihirbazı KP-UI-2, sayfa tasarımcısı KP-UI-3 — bu pakette YOK. Mockup kalanları #1 (UAS-001), #4 (ham kod yok), #8 (iddia metni), filtrede TM/tk/be burada kapanır.

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-UI-1-path-list-workspace-kurgu.md · …/DESIGN-KP-STUDIO-knowledge-path-studio.md · …/mockups/kp-studio/{KP-STUDIO-mockup-analysis.md, kp-studio-screens-v2.decoded.html, kp-studio-screen-texts.txt} · …/WP-KP-1-knowledge-path-studio-model.md (uçlar + hata kodları) · frontend/Diten.Web/Controllers/CRM/{KnowledgePathsController.cs, ClaimDisplayNames.cs, ClaimsController.V2.cs (lookups/countries), KnowledgeController.Claims.cs (claim-options), KnowledgeConceptsController.cs} · Views/CRM/KnowledgePaths/** · wwwroot/assets/js/CRM/KnowledgePaths/** · services/Diten.CrmService/src/**/Knowledge/Path/{KnowledgePathDtos, KnowledgePathStudio*}.cs (DTO sözleşmesi, okumak için) · memory l10n-bridge-pascalcase-loader, dt-inline-filter-host-class, updatevisualstate-global-selectors, knowledge-taxonomy-sortorder-ux.

NE:
 1) Proxy/lookup (yeni partial): bind-chain, claims add/arrange/remove izinli proxy; lookups/chain-templates (yayında+dallı; ad/sürüm/konu/ürün), lookups/countries (ClaimDisplayNames; ICU ad + diller), lookups/path-contents?pathId (yol dili + ürün/konu uyumlu yayındaki içerik, tip ETİKETİ), lookups/path-claims?pathId&q (ürün uyumlu; ülke sürümü/durum/kullanılabilirlik+gerekçe; FE-5 mantığını ortak yardımcıya al, kopyalama).
 2) Liste: UAS-001 (read yoksa düz 403, iskelet yok); sütunlar Yol, Ürün, Ülke/dil (ADLARIYLA), Zincir, Sürüm, Durum, "Onaysız eski yol" rozeti; filtreler ürün, ülke (6), dil (tr/ru/be/uz/tk/ka/az yerel ad), durum, arama, "Yalnız onaysız eski yollar"; özet kartları (toplam/taslak/yayında/onaysız eski; tıklayınca filtre); eski yol satırında "Zincire bağla" → basit bağlama modalı (zincir+ülke+dil → bind-chain; sihirbaz KP-UI-2).
 3) Yeni yol: zincir (yayında+dallı) + ülke (ad) + dil (ülkenin dilleri, yerel ad) + ad/açıklama/amaç/geçerlilik; konu/ürün/kitle salt okunur önizleme; kod boşsa sunucu KP- üretir; kaydedince çalışma alanı.
 4) Çalışma alanı kabuğu (/CRM/KnowledgePaths/{id}; Edit/Details yerine): başlık + TEK durum çizgisi; bağlam şeridi (zincir/ülke/dil kilit ikonlu "yolun kimliği; başka ülke için Uyarla" ipucu; ürün/kitle salt okunur); yalnız "Kurgu" sekmesi (boş sekme gösterme); sıradaki adım bandı + tanıtım metni ilk kullanımda, kapatılabilir (localStorage try/catch); eski yolda "Onaysız eski yol — sahada kullanılamaz · Zincire bağla" bandı.
 5) Kurgu: dal sütunları, adımlar zincirden (dal/adım ekle/çıkar/taşı YOK, not "Dal ve adımlar zincirden gelir"); adım kartı (zorunlu, süre, ön koşul, min/max + uyum durumu ok/eksik/fazla); içerik öğesi (başlık, tip etiketi, dil adı, yayın durumu); iddia öğesi (kod·ad·METİN yol dilinde·niteleyici·ülke sürümü+durum; kullanılamazsa gerekçe kullanıcı dilinde); öğeler yalnız kendi adımı içinde sürüklenerek sıralanır (ayrı liste, DataTable tbody değil) + klavye yukarı/aşağı; "Adıma ekle" paneli (İçerik/Onaylı iddia; arama; "Ekle" ya da gerekçeli "Eklenemez": dil/ürün/yayında değil/slot dolu); iddia arama (kod/metin, yolun ürünü, ülke sürümü durumu); adım ayarları (zorunlu, süre, ön koşul; min/max düzenlenemez); üstte "Sahadaki sıra" (sunucu StepOrder); taslak dışı salt okunur.
 6) Hatalar: 13 CRM kodu kullanıcı dilinde metin (ham kod yok; gerekirse ipucu).
 7) L10n/erişilebilirlik: yeni resx KnowledgePathStudio.{en,tr,fr,es,zh,ar,ru} + PascalCase köprü; ar RTL (mantıksal CSS); açık/koyu tema (tema değişkenleri, sabit hex yok); klavye + aria-label.
 8) Eski MVC Create/Edit/Details yeni ekranlara yönlensin; mevcut api/* proxy'leri korunur.
KORU/YAPMA: CRM/Platform/Auth DOKUNMA; İddialar/Bilgi İçerikleri/yolculuk ekranları DEĞİŞMEZ (ortak yardımcı dışında); yayın düğmesi YOK; zincirsiz eski yolun adım düzenleme davranışı korunur; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\kp-ui-1; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 343); Web build 0 hata; node --check temiz. Yeni testler: izin kapısı (UAS-001 düz 403), lookup'lar (yayında+dallı zincir, ülke/dil adları, içerik dil/ürün filtresi, iddia gerekçesi), proxy allowlist, 13 hata kodu → metin, L10n 7 dil + JS↔resx eşliği, ham kod görünmemesi, FE-5 claim-options değişmedi. Sabotaj: izin kapısı + iddia gerekçesi testleri kırmızıya dönmeli. Commit ("feat(crm): WP-KP-UI-1 — knowledge path studio list, workspace shell, composition" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: mevcut api/* proxy'lerinden biri başka ekranda kullanılıyor ve değişiklik onu bozacaksa DUR + raporla.
```
