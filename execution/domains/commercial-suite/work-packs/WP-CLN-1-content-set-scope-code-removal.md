# WORK PACKAGE — WP-CLN-1 · İçerik Seti / Set Revizyonu / İçerik Kapsamı kodunun kaldırılması (KP-4 sonrası temizlik)

> **CT (SoR), 2026-10-01.**
> - **Yol haritası:** `ROADMAP-content-to-visit.md` Faz 1. Kullanıcı (2026-10-01): "SB-2b ve kod temizliği paketle".
> - **Kaynak karar:** bridge-decision §7 (kapsam kaldırılır) + §8 (set Bilgi Yolu'na birleşir); DESIGN-KP-STUDIO §7.3 ("sonraki temizlik: salt okunur set / kapsam repository'leri ve class-map `LegacyScope`, veri 0 doğrulanınca").
> - **KP-4 (cc80af31) bıraktıkları:** set + revizyon okuma uçları `[Obsolete]` salt okunur; repository'ler + class-map'ler (`LegacyScope`) duruyor; Web `/CRM/ContentSets*` → 301 `/CRM/KnowledgePaths`; Auth `crm.content-set.*` / `crm.content-scope.*` "deprecated".
> - **Kapsam:** yalnız CrmService (+ testleri). Veri yazma YOK (veri temizliği CLN-2, kullanıcı onayıyla).
>
> **Çalışma yeri:** worktree `C:\tmp\cln-1`, dal `wp/cln-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Kullanılmayan İçerik Seti / Set Revizyonu / İçerik Kapsamı kodunu CRM'den tamamen kaldırmak; yalnız hâlâ anlamı olan parçaları (yol / içerik provenance'ı, dil varyantı alanı) korumak.

## Kanıt (CT)
- **API:** `Api/Controllers/CRM/ContentSetsController.cs` (`[Obsolete]` okuma, `LegacyScope` notu :15), `ContentSetRevisionsController.cs`.
- **Application:** `Features/ContentComposition/ContentSets/**`, `Features/ContentComposition/ContentSetRevisions/**`.
- **Domain:** `Entities/ContentSet.cs` (`LegacyScope` :42), `ContentSetRevision.cs` (`LegacyScope` :42), `ContentScope.cs`, `ContentSetScopeRef`; `Repositories/IContentSetRepository.cs`, `IContentSetRevisionRepository.cs`, `IContentScopeRepository.cs`.
- **Persistence:** `Repositories/ContentSetRepository.cs`, `ContentSetRevisionRepository.cs`, `ContentScopeRepository.cs`; `DependencyInjection.cs` kayıtları + class-map'ler (:648-652, :694-696 `LegacyScope` → `"Scope"`) + index oluşturma.
- **KORUNACAKLAR (dokunma):**
  - `KnowledgeContent.ContentSetId` — **dil varyantı** alanı, İçerik Seti değil (KP-4 §37);
  - `KnowledgeStudioOrigin` (`KnowledgeContent.StudioOrigin`, `KnowledgePath.StudioOrigin`, class-map :570) — üretilmiş kayıtların provenance'ı, okuma uyumu için kalır (içindeki set / revizyon kimlikleri yalnız veri);
  - `Application.Common.Artifacts` / `IContentArtifactStore` (KP-3 kullanıyor);
  - Web 301 yönlendirmesi; Auth "deprecated" anahtarları (silinmez; DESIGN-KP §7.2).

## NE
1. **Önce salt okuma veri kontrolü** (yerel Mongo, CRM veritabanı; Python + pymongo, yazma YOK): set, set revizyonu ve kapsam koleksiyonlarının tenant başına belge sayısı.
   - Beklenen: set **0**, revizyon **0**, kapsam **1** (97c5 "test").
   - Set ya da revizyon > 0 ise **DUR** + raporla (kod kaldırılırsa veri okunamaz kalır).
   - Kapsam belgesi(leri) kalır (CLN-2 kullanıcı kararıyla arşivler / siler); kod kaldırılınca okuyucusu olmaz — raporda belirt.
2. **Kaldır:** iki controller, iki feature klasörü (komut / sorgu / handler / DTO / mapper / doğrulama), üç entity + `ContentSetScopeRef`, üç repository + arayüz, DI kayıtları, class-map'ler, index oluşturma; bunlara ait testler.
   - Başka bir özellik bu tiplere bağlıysa (derleme hatası) **bağımlılığı raporla**; anlamlı bir kullanım varsa kaldırma, DUR.
   - `KnowledgeStudioOrigin` set / revizyon tiplerine **tip** olarak bağlıysa yalnız Guid / string alan olarak kalacak şekilde sadeleştir (BSON şekli değişmez; round-trip testi).
3. **Auth / manifest:** dokunma (KP-4'te yapıldı). Varsa CRM tarafında set / kapsam izin sabitleri yalnız kaldırılan kodda kullanılıyorsa kaldır; Auth kataloğu değişmez.
4. **Belge:** bridge-decision §6 tablosunda madde 9 ve DESIGN-KP §7.3'e "CLN-1 ile kaldırıldı" notu.

## KORU / YAPMA
- **Veri yazma / silme YOK** (koleksiyonlar düşürülmez; index silinmez — yalnız oluşturma kodu kalkar).
- Bilgi Yolu, revizyon, MLR, yayın, iddia kullanımı, ziyaret çözücüsü DEĞİŞMEZ.
- Web DOKUNMA (yönlendirme kalır). Platform / Auth DOKUNMA.
- PlannedVisits Web `ReadFallback` bu pakette YOK (saha-rep rol kararına bağlı, ayrı iş).
- **DUR:** set / revizyon verisi > 0; kaldırılacak tiplerin canlı bir okuyucusu çıkarsa.

## Acceptance
- **E2:** CRM 0 kırmızı (taban **2153/0/5**; kaldırılan set / kapsam testleriyle düşer — düşen testleri listele), Web 0 kırmızı (taban **422/0**), build 0 hata (yeni uyarı yok; `CS0618` pragmaları da kalkar).
  - `grep` kanıtı: `ContentSetRepository|ContentScopeRepository|ContentSetRevisionRepository|LegacyScope|ContentSetsController|ContentSetRevisionsController` üretim kodunda 0.
  - **Testler:** `KnowledgeStudioOrigin` round-trip (eski belge okunur); `KnowledgeContent.ContentSetId` round-trip; iddia kullanımı + KP-3 render / yayın testleri yeşil.
  - **Sabotaj:** `KnowledgeStudioOrigin` class-map'inden bir alan kaldırılınca round-trip testi kırmızı.
- **E4 (CT):** fleet sonrası CRM ayağa kalkar (index / class-map hatası yok); Bilgi Yolları ve İddialar sayfaları açılır.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-CLN-1 · İçerik Seti / Set Revizyonu / İçerik Kapsamı kodunun kaldırılması (KP-4 sonrası temizlik)
Repository: C:\tmp\cln-1 (worktree) · Branch: wp/cln-1 · commit bu dala, push YOK · yalnız CrmService (+ testleri)

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-CLN-1-content-set-scope-code-removal.md — önce tamamını oku. Ayrıca: …/WP-KP-4-retire-content-set-scope.md (§37) · …/SCMM-studio-knowledge-bridge-decision.md §6-§8 · …/DESIGN-KP-STUDIO-knowledge-path-studio.md §7 · memory crm-classmap-rejects-unknown-elements, crm-new-aggregate-classmap-guid.

NE: (1) Önce SALT OKUMA veri kontrolü (Python+pymongo, yerel Mongo CRM DB): set / set revizyonu / kapsam koleksiyonlarında tenant başına sayı. Beklenen set 0, revizyon 0, kapsam 1 (97c5 "test"). Set ya da revizyon >0 → DUR. (2) Kaldır: ContentSetsController, ContentSetRevisionsController, Features/ContentComposition/ContentSets + ContentSetRevisions, ContentSet / ContentSetRevision / ContentScope entity + ContentSetScopeRef, 3 repository + arayüz, DI kayıtları, class-map'ler (LegacyScope), index oluşturma kodu, ilgili testler. KnowledgeStudioOrigin set tiplerine bağlıysa Guid/string alanla sadeleştir (BSON şekli aynı). (3) Auth/manifest DOKUNMA; CRM'de yalnız kaldırılan kodun kullandığı izin sabitleri kalkar. (4) bridge-decision §6 madde 9 + DESIGN-KP §7.3'e "CLN-1 ile kaldırıldı" notu.
KORU/YAPMA: KnowledgeContent.ContentSetId (dil varyantı), KnowledgeStudioOrigin (provenance), Application.Common.Artifacts KORUNUR; veri yazma/silme, koleksiyon/index düşürme YOK; Web/Platform/Auth DOKUNMA; PlannedVisits ReadFallback bu pakette YOK.
DOĞRULA (E2): cd C:\tmp\cln-1; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2153/0/5, düşen testleri listele); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 422); build 0 hata, yeni uyarı yok. grep kanıtı (üretim kodu 0): ContentSetRepository|ContentScopeRepository|ContentSetRevisionRepository|LegacyScope|ContentSetsController|ContentSetRevisionsController. Testler: KnowledgeStudioOrigin + ContentSetId round-trip; iddia kullanımı + KP-3 render/yayın yeşil. Sabotaj: KnowledgeStudioOrigin class-map'inden bir alan kaldır → round-trip kırmızı. Commit ("refactor(crm): WP-CLN-1 — remove content set / set revision / content scope code (read-only remnants of KP-4)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: set/revizyon verisi >0 ya da kaldırılacak tiplerin canlı bir okuyucusu varsa → DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-01) — **ACCEPTED (E2)**
- **Commit:** ajan `525d88ae` (taban `9bbca6a4`) → `test/crm-content-visit-e2e` fast-forward. 32 dosya (+109 / −1711); 22 dosya silindi (2 controller, 2 feature klasörü, 3 entity, 3 repository + 3 arayüz).
- **Veri (ajan, salt okuma, `DitenERP_Dev`):** `content_sets` 0, `content_set_revisions` yok, `content_scopes` 1 (97c5 "test", `SCOPE-2026-395306`) → DUR yok; kapsam belgesi CLN-2'ye.
- **Diff (K13 okuma):**
  - DI kayıtları, class-map'ler (`LegacyScope` dahil) ve index oluşturma kaldırıldı; koleksiyon / index / veri dokunulmadı.
  - Paylaşılan `ChainContextErrors` silinen `ContentSet.cs`'den `Entities/ChainContextErrors.cs`'e taşındı, kodlar aynı.
  - `KnowledgeStudioOrigin` yalnız XML notu değişti (alanlar zaten Guid; BSON şekli aynı).
  - `KnowledgeContent.ContentSetId` + `KnowledgeStudioOrigin` round-trip testleri eklendi.
- **CT testleri:** CRM **2147/0/5** (−11 set testi, +5 yeni; ilk koşuda bilinen sıra flake'i 1 kez kırmızı, ikinci koşu temiz), Web **422/0**.
- **CT sabotajı:** taşınan `ChainContextErrors.LanguageNotInCountry` değeri değiştirildi → Web 1 + CRM 2 kırmızı. Kod geri alındı. Ajan: `KnowledgeStudioOrigin` class-map alanı → round-trip kırmızı.
- **Sapma (kabul):** "Web DOKUNMA"ya rağmen `KnowledgePathStudioWebTests` içinde CRM kaynak dosya yolu `ContentSet.cs` → `ChainContextErrors.cs` (yalnız test; Web çalışma kodu aynı). Yanıltıcı adla dosya bırakmaktan doğru.
- **E4 (CT, Faz 0):** fleet sonrası CRM ayağa kalkar; Bilgi Yolları + İddialar sayfaları açılır.
