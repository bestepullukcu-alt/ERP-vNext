# WORK PACKAGE — WP-CT-L10N-FULL · Kavram Grafiği resx'ini 7 dilde tamamla (TR Türkçe karakter + 5 dilde İngilizce kalanlar) (frontend, resx-only)

> **CT (SoR).** MOD-0162 (Kavram Grafiği: tipler, düğümler, bağlantılar, zincir şablonları, grafik önizleme). Branch `feature/crm-chain-template`. **WP-CT-FE-10 commit'inden SONRA** dispatch edilir (FE-10 aynı resx'e 3 anahtar ekliyor — çakışma olmasın). **Kaynak:** WP-CT-E4-LIVE bulgu #7 + L10N-LEGACY'de açılan borç `F-CONCEPTS-L10N-FULL`. **Kullanıcının arayüz dili TR** — TR'deki karaktersiz metinler ("Zincir Sablonu Olustur", "Iptal", "Dugum") her ekranda görünüyor. **Yalnız resx `<value>`**; kod/köprü/JS/backend DEĞİŞMEZ.

## Kanıt (CT ölçümü, 2026-09-27, FE-10 öncesi 266 anahtar)
- Dosyalar: `frontend/Diten.Web/Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (bu klasördeki tüm view'lar — Index, node Create/Edit/Details, TemplateCreate/Edit, offcanvas'lar, grafik önizleme — bu tek resx ailesini kullanır).
- **TR:** 194 değerde Türkçe karakter yok; bunların önemli kısmı **ASCII'ye çevrilmiş Türkçe** (ör. `ConceptsTitle` "Kavram Grafigi", `ConceptNodeName` "Dugum Adi", `Description` "Aciklama", `EffectiveFrom` "Gecerlilik Baslangici", `ExternalRefType` "Dis Referans Tipi", `PageDescription` "…grafigini yapilandirin: tipler, dugumler, baglantilar ve zincir sablonlari."). Bir kısmı ise zaten doğru ("Kavramlar", "Konu", "Durum") — dokunulmaz.
- **fr/es/zh/ar/ru:** değeri en ile aynı olan anahtar sayısı fr 136 · es 128 · zh 127 · ar 127 · ru 127 (genel konsol: Save/Cancel/Actions/Active/Search/Export…, bölüm başlıkları `IdentitySection`/`ReferenceSection`, sekme adları `TypesTab/NodesTab/ConnectionsTab/TemplatesTab/GraphPreviewTab`, sayfa başlığı `ConceptsTitle` "Concept Graph", tip/düğüm/ilişki alanları, Graph* anahtarları, GlobalProduct* anahtarları…).
- **RU terim ayrışması** (L10N-LEGACY raporu): "kavram" = понятие ×7 / концепт ×1 (`ConceptType` "Тип концепта").

## NE (yalnız `<value>`)
1. **TR — Türkçe karakter geri yükleme:** ASCII'ye çevrilmiş her TR değeri doğru Türkçe yazımla düzelt (ç ğ ı İ ö ş ü). Yalnız yazım düzeltmesi + doğal Türkçe; **anlam/terim değiştirme**. Zaten doğru olanlara dokunma. Terimler: Kavram Grafiği · Düğüm · Bağlantı · İlişki · Zincir Şablonu · Omurga · Dal · Adım · Sürüm · Yayında · Taslak · Arşiv · Geçerlilik · Dış Referans · Açıklama · İptal · Oluştur · Düzenle · Görüntüle.
2. **fr/es/zh/ar/ru — İngilizce kalan tüm değerleri çevir** (FE-10'un eklediği `ChainStatus*` dahil kontrol et; zaten çevrilmişse dokunma).
   - Fransızcada en ile **meşru aynı** yazılanlar DOKUNMA: `Description`, `Branches`, `BranchCount`, `TabVersions`, `TypePalette`, `ForWhom`, `MinSelection`, `MaxSelection` + gerçekten aynı yazılan diğerleri (ör. "Actions", "Import", "Export", "Direction", "Conformance", "Global Product" gibi FR/ES'te aynı/uluslararası olanları **gerekçesiyle** raporla).
   - Teknik kısaltma/kod içerenler (`MetadataJson` "Metadata (JSON)", "ATC", "ID") anlamlı biçimde çevrilir; kısaltma kalır.
3. **Terim tutarlılığı (dosya içi, ZORUNLU):** aynı kavram her anahtarda aynı kelime. Referans sözlük (v2 anahtarlarında zaten kullanılan):
   | Kavram | FR | ES | ZH | AR | RU |
   |---|---|---|---|---|---|
   | Subject/Konu | Sujet | Tema | 主题 | الموضوع | Тема |
   | Concept | concept | concepto | 概念 | مفهوم | **понятие** (tek terim) |
   | Concept type | type de concept | tipo de concepto | 概念类型 | نوع المفهوم | тип понятия |
   | Node | nœud | nodo | 节点 | عقدة | узел |
   | Connection/Relationship | relation | relación | 关系 | علاقة | связь |
   | Chain template | modèle de chaîne | plantilla de cadena | 链模板 | قالب السلسلة | шаблон цепочки |
   | Spine | ossature | columna | 主干 | العمود الفقري | каркас |
   | Branch | branche | rama | 分支 | فرع | ветвь |
   | Step | étape | paso | 步骤 | خطوة | шаг |
   Dosyada mevcut v2 çevirisi bu tablodan farklıysa **mevcut v2 çevirisine uy** ve raporla (RU "понятие" hariç — o karar: понятие; `ConceptType` "Тип концепта" → "Тип понятия").
4. `{0}`/`{1}` yer tutucuları, HTML/entity, satır sonu, `xml:space` aynen korunur.

## KORU / YAPMA
- **Yalnız bu 7 resx dosyasının `<value>`'ları.** `en` DOKUNMA (kaynak dil). Anahtar ekleme/silme/yeniden adlandırma YOK (anahtar sayısı FE-10 sonrası neyse o, 7 dilde eşit); sıra/CRLF/`xml:space` korunur.
- Köprüler (`_IndexL10n`, `_TemplateFormL10n`), JS, cshtml, controller, backend DEĞİŞMEZ. Değer anahtar adını echo'lamaz; boş değer YOK.
- Başka modüllerin resx'leri (Knowledge Subjects/Topics/AudienceProfile, SCMM, NavL10n/sidebar) bu WP'de YOK — görülen TR karakter sorunlarını **liste halinde raporla** (ayrı WP).
- **DUR:** bir değerin Türkçe/hedef dil anlamı belirsizse (alan terimi) → en yakın doğal karşılık + raporda işaretle; yer tutucu sayısı en ile uyuşmayan mevcut değer bulunursa → düzelt + raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → 0 kırmızı (FE-10 sonrası sayı). git diff: yalnız 6 resx (tr/fr/es/zh/ar/ru) `<value>` satırları; en/kod diff YOK.
- **Denetim:** fr/es/zh/ar/ru'da en ile aynı kalan = yalnız raporda gerekçelendirilen meşru liste; TR'de ASCII'ye çevrilmiş Türkçe kalan = 0 (rapor: önce/sonra örnek 15 değer); 7 dilde anahtar seti eşit; `{n}` tutarlı; RU'da "концепт" geçen değer = 0.
- **E4 (FLEET RESTART — resx):** TR: Kavram Grafiği sayfa başlığı/sekmeler/Zincir Şablonu editörü Türkçe karakterli ("Zincir Şablonu Oluştur", "İptal", "Düğüm"); FR/ES/ZH/AR/RU'dan birinde sekmeler/bölüm başlıkları/Kaydet çevrilmiş. Tarayıcı: oturum açık sekmelere mock enjekte etme, ayrı sekme, **yazma YOK**; dil değiştirildiyse sonunda **TR'ye geri al**.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-CT-FE-10 commit'inden SONRA)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-L10N-FULL · Kavram Grafiği resx'ini 7 dilde tamamla (TR Türkçe karakter + 5 dilde İngilizce kalanlar) (MOD-0162, frontend, resx-only)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout · Ön koşul: WP-CT-FE-10 commit'i dalda (aynı resx'e 3 anahtar ekledi) — yoksa DUR.

Kullanıcının arayüz dili TR ve TR resx'te Türkçe karakterler ASCII'ye çevrilmiş ("Zincir Sablonu Olustur", "Dugum", "Aciklama", "Iptal"); fr/es/zh/ar/ru'da ~127-136 değer İngilizce kalmış (Save, bölüm başlıkları, sekme adları, "Concept Graph"…). YALNIZ resx <value>; en/kod/köprü/backend DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-L10N-FULL-knowledge-concepts.md (kanıt, terim sözlüğü, meşru-aynı listesi) · frontend/Diten.Web/Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{en,tr,fr,es,zh,ar,ru}.resx · execution/domains/commercial-suite/work-packs/WP-CT-L10N-LEGACY-editor-keys.md (önceki terim kararları).

NE:
 1) TR: ASCII'ye çevrilmiş her Türkçe değeri doğru yazımla düzelt (ç ğ ı İ ö ş ü); yalnız yazım + doğal Türkçe, anlam/terim değiştirme; zaten doğru olanlara dokunma.
 2) fr/es/zh/ar/ru: en ile aynı kalan tüm değerleri çevir (ChainStatus* dahil kontrol). FR meşru-aynı DOKUNMA: Description, Branches, BranchCount, TabVersions, TypePalette, ForWhom, MinSelection, MaxSelection + gerçekten aynı yazılan diğerleri gerekçesiyle raporla.
 3) Terim tutarlılığı WP sözlüğüne göre (Subject/Concept/Concept type/Node/Relationship/Chain template/Spine/Branch/Step); dosyadaki mevcut v2 çevirisi farklıysa ona uy + raporla; RU "kavram" = понятие (tek terim) → ConceptType "Тип понятия", "концепт" kalmaz.
 4) {n} yer tutucu, HTML/entity, xml:space, CRLF aynen.
KORU/YAPMA: yalnız 6 resx (tr/fr/es/zh/ar/ru) <value>; en DOKUNMA; anahtar ekle/sil/yeniden adlandır YOK (7 dilde eşit set); köprü/JS/cshtml/controller/backend DEĞİŞMEZ; değer anahtarı echo'lamaz, boş değer yok; başka modül resx'leri YOK (gördüğün TR karakter sorunlarını liste halinde raporla). Tarayıcı: oturum açık sekmelere mock enjekte etme, ayrı sekme, yazma YOK; dil değiştirdiysen sonunda TR'ye geri al.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 0 kırmızı; git diff yalnız 6 resx <value>; denetim: 5 dilde en ile aynı kalan = yalnız gerekçeli meşru liste, TR'de ASCII Türkçe kalan 0 (önce/sonra 15 örnek), 7 dilde anahtar seti eşit, {n} tutarlı, RU'da "концепт" 0. E4 (fleet restart sonrası): TR Kavram Grafiği + Zincir Şablonu editörü Türkçe karakterli; FR/ES/ZH/AR/RU'dan birinde sekmeler/bölüm başlıkları/Kaydet çevrilmiş. Ayrı commit ("i18n(crm): WP-CT-L10N-FULL — complete knowledge concepts localization in 7 languages (TR diacritics + fr/es/zh/ar/ru) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: anlamı belirsiz alan terimi → en yakın doğal karşılık + raporda işaretle; yer tutucu sayısı en ile uyuşmayan mevcut değer → düzelt + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-27) → **ACCEPTED (E2)**
```
Commit: a502614b · Agent: PASS (229/0, TR+FR tarayıcı yazmasız) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-l10nfull @a502614b → Diten.Web.Tests 229/0
```
- ✅ **Kapsam:** yalnız 6 resx (tr/fr/es/zh/ar/ru), +763/−763; değişen satırların **tamamı `<value>`** (value-dışı = 0). en/kod/köprü diff YOK.
- ✅ **Anahtar seti:** 7 dilde 269, eşit. `{n}` uyumsuzluğu 0. Boş değer 0.
- ✅ **en ile aynı kalan:** zh/ar/ru **0**; es `No`, `Color` (İspanyolcada aynı); fr = WP meşru listesi + `ConceptsMenu` (Concepts), `Actions`, `Direction`, `NewNodeDirection` — Fransızcada doğru yazım, kabul.
- ✅ **RU:** "концепт" 0; `ConceptType` = "Тип понятия".
- ✅ **TR:** Türkçe karaktersiz kalan 60 değer tek tek okundu — hepsi doğru Türkçe (Kaydet, Konu, Omurga, Taslak, "{0}. dala ekle"…); ASCII'ye çevrilmiş kelime 0. Örnek: Kavram Grafiği · İptal · İşlemler · Zincir Şablonları · Kavram Düğümleri.
- ✅ **Agent kararları (kabul):** `AddToBranch` "{0}. dala ekle" (sayı ekinde doğru), `MoveLater` "Sonraya al" (davranışa uygun).
- ℹ **Küçük tutarsızlık (borç):** `NewVersion` "Yeni versiyon" — dosyanın geri kalanı "sürüm" diyor; sonraki L10n dokunuşunda "Yeni sürüm".
- ℹ **Diğer modüllerde TR karakter adayları (agent taraması, kaba):** Platform/AuditLog 11 · SubscriptionFeatures 9 · AuditRetention 7 · SubscriptionPlans 6 · DocumentManagement/QmsBaselines 4 · Platform/Administrators 3 · Account 2 · SharedResource 2 · CRM/TerritoryManagement 1 · CRM/Knowledge 1 → ayrı WP (kapsam dışı).

**WP-CT-L10N-FULL KOMPLE (E2).** Chain Template v2 + Kavram Grafiği L10n tamam. Sırada: main senkron + push + PR.
