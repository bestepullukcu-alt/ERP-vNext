# `.antigravity/` + Module Pack Standardı Düzeltmesi — Uygulama Özeti

**Durum:** ✅ Uygulandı (2026-05-12)
**Tetikleyici:** NEW-002 Platform Administrators module pack hazırlığında 10 sistemik hata yakalandı (layout zorunluluğu eksik, handler folder yapısı muğlak, naming çelişkileri).
**Kök neden:** `.antigravity/` kural dosyaları gerçek Golden Reference Slim/Compact koduyla uyumsuzdu — module pack hazırlayan AI hangi dokümanı okursa farklı sonuç çıkarıyordu.

> **Bu dosya audit özetidir.** Detaylı uygulama planı için: `/Users/alitufanoglu/.claude/plans/rosy-knitting-token.md`

---

## Çözüm Stratejisi

**Tek gerçek standart:** Golden Reference Slim (DEV-0000) / Compact (DEV-0001) — gerçek çalışan kod. Tüm doküman ve kurallar bu koda eşitlendi.

- Slim referans backend: `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`
- Slim referans frontend: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`
- Compact referans: aynı klasörlerin `Compact` versiyonları
- Pack-of-record: `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md` ve `DEV-0001-golden-reference-compact.md`

---

## Yapılan Değişiklikler (10 Dosya)

### `.antigravity/rules/`
1. **`module-pack-standard.md`** — Tam yeniden yazıldı
   - Frontmatter'a 4 yeni zorunlu alan: `service`, `shell`, `golden_reference`, `entity_base`
   - 7 yeni zorunlu bölüm: Layout & Shell Contract, Backend File Convention, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Ready-for-dev Checklist
   - Golden Reference kontratı (folder/naming birebir kopyala kuralı)
   - Numbering hataları düzeltildi
   - Quick Template güncellendi

2. **`erp-architecture.md`** — Action-Based Separation ve permission format düzeltildi
   - Folder yapısı: `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/` (5 klasör)
   - Handler naming: suffix YOK (`{Verb}{Module}Handler`)
   - Permission format iki kabul: `Platform.*` (Diten.Platform) / `Modules.*` (tenant servisleri)

3. **`handler-design.md`** — Tüm `*Request*` örnekleri `*Command*` olarak güncellendi
   - Naming standardı notu eklendi (suffix YOK)

4. **`response-envelope.md`** — Naming Request→Command çevirildi
   - `CreateProductRequest` → `CreateProductCommand`
   - `CreateProductRequestHandler` → `CreateProductHandler`

5. **`frontend-datatable-template.md`** — Yanıltıcı default kaldırıldı
   - L175 ve L292: `_LayoutTenantShell` default'u → `{{LayoutName}}` placeholder, shell alanından türetilir

6. **`entity-base-template.md`** — Sınıf adı notu eklendi (servis bazlı)
   - EntityBase / BaseEntity / GlobalEntity ayrımı

### `.antigravity/agents/`
7. **`module-pack-author.md`** — Tam yeniden yazıldı
   - Zorunlu bağlam okuma 11 dosya (Golden Reference pack + gerçek kod zorunlu)
   - Çıktı listesi 20 madde (yeni 7 bölüm dahil)
   - Anti-pattern listesi

### `.antigravity/workflows/`
8. **`prepare-module-pack.md`** — Bağlam okuma 4'ten 11 dosyaya genişletildi, Ready-for-dev checklist eklendi

### `.antigravity/`
9. **`PROMPT-GUIDE.md`** — Backend CQRS Standart Promptu'na naming kuralları + Models.cs eklendi

### `docs/platform/`
10. **`master-plan.md`** — Naming + folder + EntityBase notu + NEW-002 GlobalEntity düzeltmesi
    - Dosya şablonu (L106-141): Golden Reference birebir folder yapısı
    - Naming Conventions (7.1): Command/Query record, Handler/Validator class (suffix YOK)
    - Action-Based Separation (7.2): 5 klasör + Models.cs tek dosya
    - EntityBase notu (7.3): servis bazlı sınıf adı
    - NEW-002 entity: `: GlobalEntity` (EntityBase değil)

11. **`platform/administrators-management/platform-administrators-implementation-plan.md`** — Architecture Decisions + Phase 4/5/9/Verification güncellendi
    - Architecture Decisions tablosu: Golden Reference referansları + shell + Layout zorunluluğu
    - Phase 4 (Commands): folder yapısı Slim birebir + naming suffix YOK
    - Phase 5 (Queries): folder yapısı + `PlatformAdministratorsModels.cs` TEK dosya
    - Phase 9 (Frontend): Layout AÇIKÇA zorunluluğu + `_DetailsQuickView.cshtml` partial'ı
    - Verification: grep ile Layout kontrolü, folder yapısı kontrolü, handler naming suffix kontrolü

---

## Etki

- **Kod etkisi:** Sıfır. Sadece dokümantasyon/standart değişiklikleri.
- **Mevcut pack'lere etki:** PSS-005/006/007/008, MOD-0043/0044/0046, MOD-0297/0298, NEW-002 hepsi yeni standardın gerisinde kaldı. Sıradaki güncellemede ya da yeni modül ekleneceği zaman güncellenebilir.
- **Yeni modüller:** Hazırlanan **her** module pack otomatik doğru çıkacak — Golden Reference + standart artık eşit.

---

## Sıradaki Adım

**Phase 8** (planda yazılı): Başka AI module-pack-author çağrılarak NEW-002 pack'i yeniden hazırlanır. Hazır prompt: `/Users/alitufanoglu/.claude/plans/rosy-knitting-token.md` → "Phase 8 — Module Pack'i Yeniden Hazırla".

**Phase 9** (planda yazılı): Claude regression test (45 madde checklist) koşar.
