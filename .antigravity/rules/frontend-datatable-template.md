# GOLDEN RULE: Standard DataTable UI Blueprint

Bu şablon, Diten ERP vNext projelerindeki tüm standart "Liste/CRUD" sayfaları (Örn: Countries, Cities, Currencies) için ZORUNLU HTML/Razor şablonudur.

> ⚠️ **MANDATES:**
> - HTML iskeletine hiç dokunma. Sadece `{{DeğişkenAdlar}}` kısımlarını doldur.
> - `<partial name="_Filter" />` her DataTable sayfasında **zorunludur** — kaldırma veya in-line yazma.
> - JavaScript başlatma için `DtDefaults.create()` zorunludur — bakınız `frontend-js-standard.md`.
> - `_Filter.cshtml` inline collapse filter bar standardına uymalıdır (bkz. aşağıdaki minimum şablon).
> - **DataTable v2 Standard Marker:** Yeni standartları uygulayan sayfalarda `<table ... data-dt-standard="v2" id="...">` zorunludur.
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
- **savedView içine kaydedilenler:** filters + search + colVis + sorting
- **kaydedilmeyenler:** page number + pageLength
- Panel açık/kapalı durumu persist edilmez.
- **Storage Key Standardı:** `dt:view-default:{tenantId}:{userId}:{module}:{tableId}`
  - `tableId` = `<table id="...">` zorunludur (çoklu DataTable çakışmasını engeller).

### Dirty-State (Save View görünürlük kuralı)
- `isDirty = normalize(currentState) != normalize(savedView || baselineDefault)`
- Save View görünürlüğü **Apply beklemez**:
  - filter değişimi (staged dahil)
  - search değişimi (**immediate apply**)
  - colVis değişimi (**immediate apply**)
  - sorting değişimi (**immediate apply**; standart default **single-sort**, multi-sort ancak explicit)
- Apply: tabloyu günceller + paneli kapatır; Save View görünürlüğünü değiştirmez.
- Reset: savedView varsa ona döner, yoksa baseline’a döner → `isDirty=false` → Save View gizlenir.

### normalize() Standardı (Mekanik)
- `null | undefined | ''` → `''`
- string: `trim()`
- filter values: primitive → string normalize
  - `1` ve `"1"` eşdeğer kabul edilir
  - boolean → `"true"` / `"false"`
- colVis: index-based `Array<boolean>` (runtime mutation varsa explicit override zorunlu)
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

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-6 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h4 class="mb-1">@Localizer["{{ModuleName}}Title"]</h4>
        {{!-- PageDescription key'i .resx dosyasında ZORUNLU tanımlanmalıdır. Hardcoded alt başlık YASAKTIR. --}}
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
    <script>
        // ── L10n Bridge ─────────────────────────────────────────────────────
        window.L10n = window.L10n || {};
        window.L10n.Active              = @Json.Serialize(SharedLocalizer["Active"].Value);
        window.L10n.Passive             = @Json.Serialize(SharedLocalizer["Passive"].Value);
        window.L10n.Unknown             = @Json.Serialize(SharedLocalizer["Unknown"].Value);
        window.L10n.Actions             = @Json.Serialize(Localizer["Actions"].Value);
        window.L10n.Edit                = @Json.Serialize(Localizer["EditBtn"].Value);
        window.L10n.ViewDetails         = @Json.Serialize(SharedLocalizer["ViewDetails"].Value);
        window.L10n.QuickView           = @Json.Serialize(Localizer["QuickView"].Value);
        window.L10n.Search              = @Json.Serialize(SharedLocalizer["Search"].Value);
        window.L10n.Export              = @Json.Serialize(SharedLocalizer["Export"].Value);
        window.L10n.Import              = @Json.Serialize(SharedLocalizer["Import"].Value);
        window.L10n.ComingSoon          = @Json.Serialize(SharedLocalizer["ComingSoon"].Value);
        window.L10n.Filter              = @Json.Serialize(SharedLocalizer["Filter"].Value);
        window.L10n.Apply               = @Json.Serialize(SharedLocalizer["Apply"].Value);
        window.L10n.SaveView            = @Json.Serialize(SharedLocalizer["SaveView"].Value);
        window.L10n.SelectStatus        = @Json.Serialize(SharedLocalizer["SelectStatus"].Value);
        window.L10n.Status              = @Json.Serialize(SharedLocalizer["Status"].Value);
        window.L10n.Reset               = @Json.Serialize(SharedLocalizer["Reset"].Value);
        window.L10n.Print               = @Json.Serialize(SharedLocalizer["Print"].Value);
        window.L10n.PDF                 = @Json.Serialize(SharedLocalizer["PDF"].Value);
        window.L10n.Copy                = @Json.Serialize(SharedLocalizer["Copy"].Value);
        window.L10n.ShowAll             = @Json.Serialize(SharedLocalizer["ShowAll"].Value);
        window.L10n.ColumnVisibility    = @Json.Serialize(SharedLocalizer["ColumnVisibility"].Value);
        window.L10n.AddNew{{ModuleName}} = @Json.Serialize(Localizer["AddNew{{ModuleName}}"].Value);
        window.L10n.DtNoRecords         = @Json.Serialize(SharedLocalizer["DtNoRecords"].Value);
        window.L10n.DtInfo              = @Json.Serialize(SharedLocalizer["DtInfo"].Value);
        window.L10n.DtInfoEmpty         = @Json.Serialize(SharedLocalizer["DtInfoEmpty"].Value);
        window.L10n.DtInfoFiltered      = @Json.Serialize(SharedLocalizer["DtInfoFiltered"].Value);
        window.L10n.DtZeroRecords       = @Json.Serialize(SharedLocalizer["DtZeroRecords"].Value);
        window.L10n.DtEmptyTable        = @Json.Serialize(SharedLocalizer["DtEmptyTable"].Value);
        window.L10n.BulkDelete          = @Json.Serialize(SharedLocalizer["BulkDelete"].Value);
        window.L10n.BulkDeleteConfirm   = @Json.Serialize(SharedLocalizer["BulkDeleteConfirm"].Value);
        window.L10n.BulkDeleteSuccess   = @Json.Serialize(SharedLocalizer["BulkDeleteSuccess"].Value);
        window.L10n.ClearSelection      = @Json.Serialize(SharedLocalizer["ClearSelection"].Value);
        window.L10n.SelectedCount       = @Json.Serialize(SharedLocalizer["SelectedCount"].Value);
        window.L10n.AreYouSure          = @Json.Serialize(SharedLocalizer["AreYouSure"].Value);
        window.L10n.Cancel              = @Json.Serialize(SharedLocalizer["Cancel"].Value);
        // DİNAMİK L10N — Modüle özgü ek key'leri buraya ekle
        {{DynamicL10nScripts}}
    </script>

    <script src="~/assets/js/{{AreaName}}/{{ModuleName}}/index.js" asp-append-version="true"></script>
}
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
        <div class="pt-2 pb-3">
            <form class="m-0" id="filterForm">
                <div class="dt-filter-bar d-flex flex-wrap align-items-center gap-2">
                    {{!-- Modüle özgü filtre alanları buraya (Select2, date range, vb.) --}}
                    <div class="filter-chip user_plan"></div>
                    <div class="filter-chip user_status"></div>

                    <div class="ms-auto d-flex gap-2">
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
        <div class="border-bottom"></div>
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
