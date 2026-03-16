# GOLDEN RULE: Form Page Template (Create/Edit)

Bu şablon, Diten ERP vNext projelerindeki tüm standart **Create/Edit** form sayfaları için (Razor + Sneat PRO) zorunlu iskelettir.

> ⚠️ **MANDATES**
> - `Layout = "_LayoutBackbone";` zorunludur.
> - Form sayfalarında `row g-6` boşluğu standarttır.
> - `col-lg-10 mx-auto` kullanılmaz. Kartlar `col-12` içinde tam genişlikte olmalıdır.
> - Görünen tüm metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden gelmelidir.
> - Form `novalidate` ile çalışır; validation feedback Bootstrap 5 `invalid-feedback` ile yapılır.

---

## Create/Edit.cshtml Şablonu

```cshtml
@model Diten.Web.Models.{{ModelName}}ViewModel
@using Diten.Web.Views.{{AreaName}}.{{ModuleName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}Index> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer
@{
    var isEditMode = Model != null && Model.Id.HasValue;
    ViewData["Title"] = isEditMode ? Localizer["EditTitle"].Value : Localizer["CreateTitle"].Value;
    Layout = "_LayoutBackbone";
}

@section Styles {
    <link rel="stylesheet" href="~/assets/vendor/libs/select2/select2.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/vendor/libs/flatpickr/flatpickr.css" asp-append-version="true" />
}

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-4 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h4 class="mb-1">@(isEditMode ? Localizer["EditTitle"] : Localizer["CreateTitle"])</h4>

        <nav aria-label="breadcrumb" class="text-muted">
            <ol class="breadcrumb mb-0 py-0">
                <li class="breadcrumb-item"><a href="/">@Localizer["BreadcrumbHome"]</a></li>
                <li class="breadcrumb-item"><a href="javascript:void(0);">@Localizer["Breadcrumb{{AreaName}}"]</a></li>
                <li class="breadcrumb-item"><a asp-action="Index">@Localizer["{{ModuleName}}Title"]</a></li>
                <li class="breadcrumb-item active text-primary">@(isEditMode ? Localizer["EditTitle"] : Localizer["CreateTitle"])</li>
            </ol>
        </nav>
    </div>

    <div class="d-flex align-content-center flex-wrap gap-4">
        <a asp-action="Index" class="btn btn-label-secondary">@SharedLocalizer["Cancel"]</a>
        <button type="submit" form="form{{ModuleName}}" class="btn btn-primary">
            @(isEditMode ? SharedLocalizer["Update"] : SharedLocalizer["Save"])
        </button>
    </div>
</div>

<div class="row">
    <div class="col-12">
        <form asp-action="@(isEditMode ? \"Edit\" : \"Create\")" method="post" id="form{{ModuleName}}" novalidate>
            @Html.AntiForgeryToken()
            <input type="hidden" asp-for="Id" />

            <div asp-validation-summary="ModelOnly" class="alert alert-danger mb-6 d-flex align-items-center" role="alert">
                <i class="bx bx-error-alt me-2"></i>
                <div>
                    <ul class="mb-0"><li asp-validation-summary="ModelOnly"></li></ul>
                </div>
            </div>

            <div class="row g-6">
                {{!-- Kartlar (field grouping) buraya. Örn:
                <div class="col-12 col-lg-6">
                    <div class="card h-100">
                        <div class="card-header">
                            <h5 class="card-title mb-0 d-flex align-items-center">
                                <i class="bx {{CardIcon}} me-2"></i>@Localizer["CardTitle"]
                            </h5>
                        </div>
                        <div class="card-body">
                            <div class="mb-6">
                                <label asp-for="Name" class="form-label">@Localizer["Name"] <span class="text-danger">*</span></label>
                                <input asp-for="Name" class="form-control" required />
                                <span asp-validation-for="Name" class="invalid-feedback"></span>
                            </div>
                        </div>
                    </div>
                </div>
                --}}
            </div>
        </form>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script src="~/assets/vendor/libs/select2/select2.js" asp-append-version="true"></script>
    <script src="~/assets/vendor/libs/flatpickr/flatpickr.js" asp-append-version="true"></script>
    <script src="~/assets/js/{{AreaName}}/{{ModuleName}}/create.js" asp-append-version="true"></script>
}
```

---

## `create.js` Şablonu

```javascript
/**
 * {{ModuleName}} – Create/Edit Page Script
 * JS-002: Module Pattern (IIFE)
 */
'use strict';

const {{ModuleName}}FormManager = (function () {
    const initSelect2 = () => {
        const select2Elements = $('.select2');
        if (!select2Elements.length) return;

        select2Elements.each(function () {
            const $el = $(this);
            $el.wrap('<div class="position-relative"></div>').select2({
                placeholder: $el.find('option[value=\"\"]').text() || '',
                dropdownParent: $el.parent()
            });
        });
    };

    const initFlatpickr = () => {
        document.querySelectorAll('.flatpickr-date').forEach((el) => {
            el.flatpickr({ monthSelectorType: 'static' });
        });
    };

    const initInputRestrictions = () => {
        document.querySelectorAll('.phone-mask').forEach((el) => {
            el.addEventListener('input', function () {
                this.value = this.value.replace(/[^0-9+\\-()\\s]/g, '');
            });
        });

        document.querySelectorAll('.numeric-only').forEach((el) => {
            el.addEventListener('input', function () {
                this.value = this.value.replace(/[^0-9]/g, '');
            });
        });
    };

    const init = () => {
        initSelect2();
        initFlatpickr();
        initInputRestrictions();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => {{ModuleName}}FormManager.init());
```
