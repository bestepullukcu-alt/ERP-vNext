# GOLDEN RULE: Standard DataTable UI Blueprint

Bu şablon, Diten ERP vNext projelerindeki tüm standart "Liste/CRUD" sayfaları (Örn: Countries, Cities, Currencies) için ZORUNLU HTML/Razor şablonudur.

> ⚠️ **MANDATES:**
> - HTML iskeletine hiç dokunma. Sadece `{{DeğişkenAdlar}}` kısımlarını doldur.
> - `<partial name="_Filter" />` her DataTable sayfasında **zorunludur** — kaldırma veya in-line yazma.
> - JavaScript başlatma için `DtDefaults.create()` zorunludur — bakınız `frontend-js-standard.md`.
> - `_Filter.cshtml` inline collapse filter bar standardına uymalıdır (bkz. aşağıdaki minimum şablon).
> - L10n bridge için `Index.cshtml` içine uzun `window.L10n.Key = ...` blokları yazılmaz; `_IndexL10n.cshtml` partial'ı JSON payload üretir, `index.l10n.js` bunu `window.L10n` içine merge eder.
> - **DataTable v2 Standard Marker:** Yeni standartları uygulayan sayfalarda `<table ... data-dt-standard="v2" id="...">` zorunludur.
> - **Toolbar Badge Clipping:** Filter/ColVis badge’leri `top-0 end-0 translate-middle` ile dışarı taşar; bu normaldir. Mobil/tablet’te kesilmemesi için çözüm `backbone-custom.css (MOD-0022)` içindeki **top safe-area padding**’dir. Sayfa bazlı “badge’i içeri taşı” veya “sadece z-index” hack’i YASAKTIR.
> - **Shared CSS Placement:** Toolbar / inline filter / Select2 chip görünümleri page-level `@section Styles` içinde tekrar edilmez; ortak kurallar `wwwroot/assets/css/backbone-custom.css` içinde tutulur.
> - **`{AreaName}` = klasör gruplaması (Örn: `MDM`, `Identity`), ASP.NET Areas routing DEĞİLDİR.**
>   - ✅ DOĞRU: `Views/MDM/Countries/Index.cshtml`
>   - ❌ YANLIŞ: `Areas/MDM/Views/Countries/Index.cshtml`

---

## ✅ DataTable v2 State Standard (Re-usable Contract)

Bu bölüm, **tüm yeni DataTable liste sayfalarında** (data-dt-standard="v2") uygulanması gereken state/persistence sözleşmesidir.

### State Modeli (Tanımlar)
- **baselineDefault:** savedView yokken referans alınan temiz başlangıç state’i.
  - filters: `''` (boş)
  - search: `''` (boş)
  - colVis: DataTable init default görünürlük (index-based)
  - sorting: DataTable init default order (single-sort)
  - pageLength: DataTable init default length (**yalnız referans**, compare/persist dışı)
- **currentState (UI / staged):** ekranda şu an seçili değerler (Apply basılmadan da değişebilir).
- **appliedState (effective table):** tabloya uygulanmış state (filtre parametreleri + dt search/colvis/order).
- **savedView:** kullanıcı “Save View” ile kaydettiği default view.

### Persistence Kararları
- Otomatik cache/stateSave **kullanılmaz** (2 saatlik state geri yükleme yasak).
- Sadece kullanıcı “Save View”e bastığında `savedView` persist edilir.
- Persist hedefi localStorage değildir; gateway üzerinden `/api/personalization/views` çağıran shared `personalizationClient` kullanılır.
- **savedView içine kaydedilenler:** filters + search + colVis + columnOrder + sorting
- **kaydedilmeyenler:** page number + pageLength
- Panel açık/kapalı durumu persist edilmez.
- **Personalization Context Standardı:** `moduleKey + pageKey`
  - Örnek: `moduleKey: "MDM"`, `pageKey: "LegalEntities"`
  - `tableId` = `<table id="...">` zorunludur (çoklu DataTable çakışmasını engeller).

### Dirty-State (Save View görünürlük kuralı)
- `isDirty = normalize(appliedState) != normalize(savedView || baselineDefault)`
- Save View görünürlüğü **yalnızca effective (applied) state** değişince güncellenir:
  - Filter: **Apply / Reset** sonrası (staged seçimler tek başına Save View’u tetiklemez)
  - Search: **immediate apply** (typing)
  - colVis: **immediate apply**
  - sorting: **immediate apply** (standart default **single-sort**, multi-sort ancak explicit)
- Apply: tabloyu günceller + paneli kapatır; Save View görünürlüğü **appliedState’e göre** güncellenir.
- Reset: savedView varsa ona döner, yoksa baseline’a döner → `isDirty=false` → Save View gizlenir.

### normalize() Standardı (Mekanik)
- `null | undefined | ''` → `''`
- string: `trim()`
- filter values: primitive → string normalize
  - `1` ve `"1"` eşdeğer kabul edilir
  - boolean → `"true"` / `"false"`
- colVis: index-based `Array<boolean>` (runtime mutation varsa explicit override zorunlu)
- columnOrder: `Array<number>` ve tüm kolon indekslerini tekil olarak içermelidir
- sorting: `Array<[index:number, dir:'asc'|'desc']>`; dir lower-case
- key ordering: object stringify öncesi sorted

### Refresh & Unapplied Change Davranışı
- Filtre seçilip Apply basılmadan refresh edilirse staged değişiklikler kaybolur:
  - savedView yoksa: baseline temiz state
  - savedView varsa: savedView restore

---

## ⚠️ Localization Sınıf ve Dosya Adı Convention'ı

> **KRİTİK KURAL:** Her modül için bir localization marker class oluşturulur.
> - Class adı: `{ModuleName}Index` (örn: `CountriesIndex`, `LegalEntitiesIndex`)
> - Class dosyası: `Views/{AreaName}/{ModuleName}/{ModuleName}Index.cs`
> - Resx dosyaları: `Resources/Views/{AreaName}/{ModuleName}/{ModuleName}Index.{lang}.resx`
> 
> **Class adı ve resx dosya adı BIRE BIR AYNI OLMALIDIR.** Aksi halde `IHtmlLocalizer<T>` hiçbir key'i çözemez ve raw key görünür.
>
> ❌ YANLIŞ: Class = `CountriesIndex`, Resx = `Index.en.resx`
> ✅ DOĞRU: Class = `CountriesIndex`, Resx = `CountriesIndex.en.resx`

---

## Master HTML Template

```html
@model IEnumerable<Diten.Web.Models.{{ModelName}}ViewModel>
@using Diten.Web.Views.{{AreaName}}.{{ModuleName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}Index> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer
@{
    ViewData["Title"] = Localizer["{{ModuleName}}Title"].Value;
    Layout = "_LayoutBackbone";
}

{{!-- ① ZORUNLU: Inline Filter partial'ı. _Filter.cshtml yoksa oluştur. --}}
<partial name="_Filter" />

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-4 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h4 class="mb-1">@Localizer["{{ModuleName}}Title"]</h4>
        <p class="mb-0">@Localizer["PageDescription"]</p>
    </div>
</div>

{{!-- ② Bulk Action Bar (Satır seçilince gösterilir) --}}
<div id="bulkActionBar" class="card mb-4 d-none">
    <div class="card-body d-flex align-items-center justify-content-between py-3">
        <div class="d-flex align-items-center gap-2">
            <i class="bx bx-check-circle text-primary icon-md"></i>
            <span id="bulkSelectedCount" class="fw-medium text-heading">0</span>
            <span class="text-muted" id="bulkSelectedLabel">@SharedLocalizer["SelectedCount"]</span>
        </div>
        <div class="d-flex gap-2">
            <button type="button" id="btnBulkDelete" class="btn btn-label-danger">
                <i class="bx bx-trash me-1"></i> @SharedLocalizer["BulkDelete"]
            </button>
            <button type="button" id="btnClearSelection" class="btn btn-label-secondary">
                @SharedLocalizer["ClearSelection"]
            </button>
        </div>
    </div>
</div>

{{!-- ③ DataTable Card --}}
<div class="card">
    <div id="skeleton-loader" class="p-4" style="display: none;">
        <div class="shimmer skeleton-row" style="width: 100%; height: 32px; margin-bottom: 2rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px; margin-bottom: 1rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px; margin-bottom: 1rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px; margin-bottom: 1rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px;"></div>
    </div>

    <div class="card-datatable table-responsive">
        <table id="dt-{{ModuleNameLower}}" data-dt-standard="v2" class="datatables-{{ModuleNameLower}} table border-top">
            <thead>
                <tr>
                    <th></th>
                    <th class="cell-fit"><input type="checkbox" class="dt-checkboxes-select-all form-check-input"></th>
                    {{TableHeaders}}
                    <th>@SharedLocalizer["Status"]</th>
                    <th class="cell-fit">@Localizer["Actions"]</th>
                </tr>
            </thead>
        </table>
    </div>
</div>

{{!-- ④ Offcanvas — Hızlı Görüntüleme (Quick View) --}}
<div class="offcanvas offcanvas-end" tabindex="-1" id="offcanvasDetailsPreview"
    aria-labelledby="offcanvasDetailsPreviewLabel" style="width: 480px;">
    <div class="offcanvas-header border-bottom">
        <div class="d-flex align-items-center">
            <div class="avatar avatar-md bg-primary-subtle text-primary rounded-circle d-flex align-items-center justify-content-center me-3">
                {{!-- Modüle özgü bir ikon seç. Örn: bx-buildings, bx-world, bx-coin, vb. --}}
                <i class="bx {{ModuleIcon}} fs-4"></i>
            </div>
            <div>
                <h5 id="oc-title" class="offcanvas-title mb-0">-</h5>
                <small id="oc-subtitle" class="text-muted">-</small>
            </div>
        </div>
        <button type="button" class="btn-close text-reset" data-bs-dismiss="offcanvas"
            aria-label="@SharedLocalizer["Cancel"]"></button>
    </div>

    <div class="offcanvas-body flex-grow-1 p-0">
        <div class="p-4">
            {{!-- Status Kutusu (her modülde sabit kalır) --}}
            <div class="bg-label-secondary rounded p-3 mb-4 d-flex align-items-center justify-content-between">
                <div>
                    <span class="d-block text-muted small fw-medium mb-1">@SharedLocalizer["Status"]</span>
                    <span id="oc-status" class="badge bg-label-secondary">-</span>
                </div>
            </div>

            {{!-- MODÜLE ÖZGÜ DETAYLAR — <dl>/<dt>/<dd> yapısını kullan. Örnek: --}}
            {{!--
            <h6 class="text-uppercase text-muted fw-bold mb-3">@Localizer["GeneralInformation"]</h6>
            <dl class="row mb-4">
                <dt class="col-5 fw-medium text-heading mb-2">
                    <i class="bx bx-rename text-muted me-2"></i>@Localizer["FieldLabel"]
                </dt>
                <dd id="oc-fieldName" class="col-7 mb-2">-</dd>
            </dl>
            --}}
        </div>
    </div>

    <div class="offcanvas-footer border-top p-4 d-flex justify-content-between">
        <button type="button" class="btn btn-label-secondary w-50 me-2" data-bs-dismiss="offcanvas">
            @SharedLocalizer["Cancel"]
        </button>
        <a id="oc-btn-edit" href="#" class="btn btn-primary w-50">@Localizer["EditBtn"]</a>
    </div>
</div>

@section Scripts {
    <partial name="_IndexL10n" />
    <script src="~/assets/js/{{AreaName}}/{{ModuleName}}/index.l10n.js" asp-append-version="true"></script>
    <script src="~/assets/js/{{AreaName}}/{{ModuleName}}/index.js" asp-append-version="true"></script>
}
```

## `_IndexL10n.cshtml` Standardı

```html
@using Diten.Web.Views.{{AreaName}}.{{ModuleName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}Index> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer

<script id="{{ModuleNameLower}}-l10n" type="application/json">
    @Json.Serialize(new
    {
        Active = SharedLocalizer["Active"].Value,
        Passive = SharedLocalizer["Passive"].Value,
        Unknown = SharedLocalizer["Unknown"].Value,
        Actions = Localizer["Actions"].Value,
        Edit = Localizer["EditBtn"].Value,
        ViewDetails = SharedLocalizer["ViewDetails"].Value,
        QuickView = Localizer["QuickView"].Value,
        Search = SharedLocalizer["Search"].Value,
        Export = SharedLocalizer["Export"].Value,
        Import = SharedLocalizer["Import"].Value,
        ComingSoon = SharedLocalizer["ComingSoon"].Value,
        Filter = SharedLocalizer["Filter"].Value,
        Apply = SharedLocalizer["Apply"].Value,
        Reset = SharedLocalizer["Reset"].Value,
        SaveView = SharedLocalizer["SaveView"].Value,
        SelectStatus = SharedLocalizer["SelectStatus"].Value,
        Status = SharedLocalizer["Status"].Value,
        Print = SharedLocalizer["Print"].Value,
        PDF = SharedLocalizer["PDF"].Value,
        Copy = SharedLocalizer["Copy"].Value,
        ShowAll = SharedLocalizer["ShowAll"].Value,
        ColumnVisibility = SharedLocalizer["ColumnVisibility"].Value,
        ColumnOrder = SharedLocalizer["ColumnOrder"].Value,
        AddNew{{ModuleName}} = Localizer["AddNew{{ModuleName}}"].Value,
        DtNoRecords = SharedLocalizer["DtNoRecords"].Value,
        DtInfo = SharedLocalizer["DtInfo"].Value,
        DtInfoEmpty = SharedLocalizer["DtInfoEmpty"].Value,
        DtInfoFiltered = SharedLocalizer["DtInfoFiltered"].Value,
        DtZeroRecords = SharedLocalizer["DtZeroRecords"].Value,
        DtEmptyTable = SharedLocalizer["DtEmptyTable"].Value,
        BulkDelete = SharedLocalizer["BulkDelete"].Value,
        BulkDeleteConfirm = SharedLocalizer["BulkDeleteConfirm"].Value,
        BulkDeleteSuccess = SharedLocalizer["BulkDeleteSuccess"].Value,
        ClearSelection = SharedLocalizer["ClearSelection"].Value,
        SelectedCount = SharedLocalizer["SelectedCount"].Value,
        AreYouSure = SharedLocalizer["AreYouSure"].Value,
        ConfirmAction = SharedLocalizer["ConfirmAction"].Value,
        DeleteConfirmationYesBtn = SharedLocalizer["DeleteConfirmationYesBtn"].Value,
        Cancel = SharedLocalizer["Cancel"].Value
        // Modüle özgü ek key'leri burada genişlet
    })
</script>
```

## `index.l10n.js` Standardı

```javascript
'use strict';

(function () {
    const payload = document.getElementById('{{ModuleNameLower}}-l10n');
    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    // ASP.NET Json.Serialize outputs camelCase keys by default.
    // JS code accesses PascalCase (e.g. L.AddNewCompany), so restore the first letter to uppercase.
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('{{ModuleName}} localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
```

---

## `_Filter.cshtml` Minimum Şablonu (Inline Collapse Filter Bar)

Her DataTable sayfasında `Views/{{AreaName}}/{{ModuleName}}/_Filter.cshtml` dosyası mevcut olmalıdır.

```html
@using Diten.Web.Views.{{AreaName}}.{{ModuleName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}Index> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer

<div id="inlineFilterHost">
    <div class="collapse" id="inlineFilterCollapse">
        <div class="pt-0 pb-3">
            <form class="m-0" id="filterForm">
                <div class="dt-filter-bar d-flex flex-wrap align-items-center gap-3">
                    {{!-- Modüle özgü filtre alanları buraya (Select2, date range, vb.) --}}
                    <div class="filter-chip filter-company-type"></div>
                    <div class="filter-chip filter-status"></div>

                    <div class="ms-auto d-flex gap-3">
                        <button type="button" class="btn btn-sm btn-primary" id="btnFilterApply">
                            @SharedLocalizer["Apply"]
                        </button>
                        <button type="reset" class="btn btn-sm btn-label-danger" id="btnFilterReset">
                            @SharedLocalizer["Reset"]
                        </button>
                    </div>
                </div>
            </form>
        </div>
    </div>
</div>
```

---

## ♿ Accessibility (A11y) Minimumları (v2)

- Icon-only toolbar butonları için `title` (tooltip) + `aria-label` zorunludur.
- Filter trigger (toolbar) için `aria-controls="inlineFilterCollapse"` + `aria-expanded` zorunludur.
- Dropdown açıldığında search input focus almalıdır (Select2).
- Focus ring kaldırma/override yasaktır.

---

## 📱 Responsive Breakpoint Standardı (v2) — Mekanik Tablo

| Breakpoint | Toolbar | Save View | Add New | Inline Filter Bar |
|---|---|---|---|---|
| `≥ 992px (lg)` | Tek satır hedef | icon + text | icon + text | triggers solda, Apply/Reset sağda |
| `768–991px (md)` | Kontrollü wrap (2 satır olabilir) | icon-only (tooltip + aria) | icon + text (gerekirse wrap) | Apply/Reset alt satıra geçebilir |
| `< 768px (sm)` | Search full-width öncelikli, action groups kontrollü | icon-only | icon-only | Apply/Reset alt satır |
| `< 576px (xs)` | Kontrollü 2–3 satır | icon-only | icon-only | Apply/Reset eşit genişlikte yan yana |
