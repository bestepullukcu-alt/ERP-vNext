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
- [ ] Single-select filter trigger: **sabit label + badge “1”** (seçili value trigger’da gösterilmez).
- [ ] Dropdown’larda search zorunlu (Select2 search enabled).
- [ ] Filter ile table arasında yalnız ince divider var; card/büyük border yok.

### 2) Toolbar / Buttons
- [ ] Toolbar hiyerarşisi korunuyor: Length, Search, Export, Import, ColVis, Filter, Save View, Add New.
- [ ] Save View default gizli; dirty-state ile görünür.
- [ ] Hover sırasında konum kayması yok (transform/translateY yasak).
- [ ] Badge clipping yok; dropdown stacking/z-index sağlam.

### 3) State Model & Persistence (v2)
- [ ] `baselineDefault` açık tanımlandı (boş filter/search + default colVis + default single-sort + default pageLength).
- [ ] `pageLength` baseline’da var ama persistence/dirty compare kapsamı dışında.
- [ ] `normalize()` mekanik kuralları uygulandı (`null/undefined/''`, trim, `1`==`\"1\"`, boolean, colVis index-based, order normalize).
- [ ] DataTables `stateSave` devre dışı (`stateSave:false`); otomatik 2 saat cache yok.
- [ ] Save View yalnız kullanıcı basınca persist eder; scope: filters + search + colVis + sorting; exclude: page number + pageLength.
- [ ] Unapplied staged filtre değişimi refresh edilince geri gelmez (savedView yoksa clean, varsa savedView restore).

### 4) Storage Key Standard
- [ ] localStorage key: `dt:view-default:{tenantId}:{userId}:{module}:{tableId}` formatında.
- [ ] `tableId` zorunlu; multi-table sayfalarda çakışma yok.

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
