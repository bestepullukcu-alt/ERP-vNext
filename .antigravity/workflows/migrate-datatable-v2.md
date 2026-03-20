---
description: "MIGRATE-DT-V2 — Mevcut DataTable Liste Sayfasını v2 Standardına Taşıma Checklist'i"
---

# /migrate-datatable-v2 — Migration Checklist (ZORUNLU)

Bu workflow, legacy DataTable liste sayfalarını **DataTable v2 standardına** (inline filter + Save View + no auto cache) taşımak için kullanılır.

> Not: v2 standardına geçen sayfalarda `<table ... data-dt-standard=\"v2\" id=\"...\">` zorunludur ve Quality Gate (quality-gate-datatable.md) eksiksiz uygulanır.

---

## ✅ Migration Checklist

### 1) UI / Layout
- [ ] Offcanvas filter kaldırıldı; toolbar altına **inline collapsible** filter panel eklendi (`#inlineFilterHost` + `#inlineFilterCollapse`).
- [ ] Filter bar “compact chip/dropdown” görünümünde; form-grid (`col-md`) kullanılmıyor; wrap destekli.
- [ ] Inline filter Select2 trigger'ları `form-select form-select-sm` estetiğine geçirildi; bu görünüm `backbone-custom.css` içindeki ortak kurallarla sağlanıyor.
- [ ] Single-select filter trigger: **sabit label + badge “1”** (seçili value trigger’da gösterilmez).
- [ ] Dropdown’larda search zorunlu (Select2 search enabled).
- [ ] Filter ile table arasında yalnız ince divider var; card/büyük border yok.
- [ ] Reusable toolbar / inline-filter / Select2 stilleri page-level `@section Styles` içinde bırakılmadı; `backbone-custom.css` içine taşındı.

### 2) Toolbar / Buttons
- [ ] Toolbar hiyerarşisi korunuyor: Length, Search, Export, Import, ColVis, Filter, Save View, Add New.
- [ ] Save View default gizli; dirty-state ile görünür.
- [ ] Hover sırasında konum kayması yok (transform/translateY yasak).
- [ ] Badge clipping yok; dropdown stacking/z-index sağlam. (Not: Clipping çözümü z-index değil; `backbone-custom.css (MOD-0022)` top safe-area padding ile sağlanır.)

### 3) State Model & Persistence (v2)
- [ ] `baselineDefault` açık tanımlandı (boş filter/search + default colVis + default single-sort + default pageLength).
- [ ] `pageLength` baseline’da var ama persistence/dirty compare kapsamı dışında.
- [ ] `normalize()` mekanik kuralları uygulandı (`null/undefined/''`, trim, `1`==`\"1\"`, boolean, colVis index-based, columnOrder, order normalize).
- [ ] DataTables `stateSave` devre dışı (`stateSave:false`); otomatik 2 saat cache yok.
- [ ] Save View yalnız kullanıcı basınca persist eder; scope: filters + search + colVis + columnOrder + sorting; exclude: page number + pageLength.
- [ ] Save View localStorage yerine shared `personalizationClient` ile `/api/personalization/views` üstünden persist edilir.
- [ ] Unapplied staged filtre değişimi refresh edilince geri gelmez (savedView yoksa clean, varsa savedView restore).

### 4) Personalization Context / Column Reorder
- [ ] `moduleKey + pageKey` context’i net tanımlandı.
- [ ] `tableId` zorunlu; multi-table sayfalarda çakışma yok.
- [ ] Reorder gerekiyorsa `ColReorder` kullanıldı; control/checkbox/actions kolonları reorder dışında bırakıldı.
- [ ] `columnOrder` capture/restore edildi ve dirty-state hesabına dahil edildi.

### 5) Accessibility (A11y)
- [ ] Icon-only butonlarda `aria-label` + lokalize tooltip/title var.
- [ ] Filter trigger `aria-controls` + `aria-expanded` doğru.
- [ ] Keyboard: Tab erişimi, dropdown focus, ESC kapanış test edildi.

### 6) Localization
- [ ] Toolbar vocabulary SharedResource üzerinden geliyor (`Search/Export/Import/Filter/Apply/Reset/ShowAll/SaveView/ColumnVisibility/...`).
- [ ] 8 dil tam; placeholder yok; hardcoded fallback yok.
- [ ] `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .` PASS.

### 7) Statik Guard
- [ ] `python3 .antigravity/scripts/verify_datatable_page.py . --area <Area> --module <Module>` PASS (v2 marker varsa v2 guard’ları da PASS).
