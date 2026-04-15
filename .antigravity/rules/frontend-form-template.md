# GOLDEN RULE: Form Page Template (Create/Edit)

Bu şablon, Diten ERP vNext projelerindeki tüm standart **Create/Edit** form sayfaları için (Razor + Sneat PRO) zorunlu iskelettir.

> ⚠️ **MANDATES**
> - `Layout = "_LayoutBackbone";` zorunludur.
> - Form sayfalarında `row g-6` boşluğu standarttır.
> - `col-lg-10 mx-auto` kullanılmaz. Kartlar `col-12` içinde tam genişlikte olmalıdır.
> - Görünen tüm metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden gelmelidir.
> - Form `novalidate` ile çalışır; validation feedback Bootstrap 5 `invalid-feedback` ile yapılır.
> - Create/Edit üst başlığı kompakt action-page standardında olmalıdır: wrapper `mb-3`, başlık `h5.mb-0`.
> - Create/Edit sayfalarında breadcrumb korunur; ancak varsayılan zincir `{{ModuleName}}Title > Current Action` olmalıdır. `Home` ve `Breadcrumb{{AreaName}}` breadcrumb'ı standart form şablonunda kullanılmaz.
> - Form sayfalarında liste ekranındaki `PageDescription` bloğu tekrar edilmez.
> - Bağımlı select (örn. `ProductType -> Category`) varsa child alan başlangıçta disabled olabilir; ancak parent seçimi sonrası child seçenekleri DOM'da yeniden oluşturulmalı, uygunsuz eski değer temizlenmeli ve select2 state'i yeniden senkronlanmalıdır. Uygunsuz seçenekleri dropdown içinde disabled/gri halde bırakmak standart dışıdır.
> - Razor tarafında boolean HTML attribute'ları için `disabled="False"` benzeri kullanım YASAKTIR. Attribute ya tamamen render edilir ya da hiç render edilmez.

---

## Create/Edit.cshtml Şablonu

```cshtml
@using Diten.Web.Views.{{AreaName}}.{{ModuleName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}Index> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer
@{
    // Edit modunda sayfa ID parametresini URL'den veya ViewBag'den alır
    var isEditMode = ViewBag.Id != null; 
    ViewData["Title"] = isEditMode ? Localizer["EditTitle"].Value : Localizer["CreateTitle"].Value;
    Layout = "_LayoutBackbone";
}

@section Styles {
    <link rel="stylesheet" href="~/assets/vendor/libs/select2/select2.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/vendor/libs/flatpickr/flatpickr.css" asp-append-version="true" />
}

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-3 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h5 class="mb-0">@(isEditMode ? Localizer["EditTitle"] : Localizer["CreateTitle"])</h5>

        <nav aria-label="breadcrumb" class="text-muted">
            <ol class="breadcrumb mb-0 py-0">
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
        <form id="form{{ModuleName}}" novalidate>
            @Html.AntiForgeryToken()
            @if(isEditMode) { <input type="hidden" id="recordId" value="@ViewBag.Id" /> }

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

    const rebuildDependentSelect = (parentEl, childEl) => {
        if (!parentEl || !childEl) return;

        const selectedParent = parentEl.value || '';
        const placeholder = childEl.querySelector('option[value=\"\"]')?.textContent || '';
        const allOptions = Array.from(childEl.options)
            .filter((option) => option.value)
            .map((option) => ({
                value: option.value,
                text: option.text,
                parent: option.dataset.parent || ''
            }));

        const syncChild = () => {
            const currentValue = childEl.value;
            const filtered = allOptions.filter((option) => option.parent === selectedParent);

            childEl.innerHTML = '';
            childEl.append(new Option(placeholder, ''));

            filtered.forEach((option) => {
                const rendered = new Option(option.text, option.value);
                rendered.dataset.parent = option.parent;
                childEl.append(rendered);
            });

            childEl.value = filtered.some((option) => option.value === currentValue) ? currentValue : '';
            childEl.disabled = !selectedParent;

            if (window.jQuery && $.fn.select2) {
                const $child = $(childEl);
                $child.prop('disabled', !selectedParent);

                if ($child.hasClass('select2-hidden-accessible')) {
                    $child.select2('destroy');
                }

                $child.select2({
                    placeholder,
                    dropdownParent: $child.parent(),
                    width: '100%',
                    allowClear: true
                }).trigger('change');
            }
        };

        parentEl.addEventListener('change', syncChild);
        if (window.jQuery && $.fn.select2) {
            $(parentEl).on('change.dependent-select', syncChild);
        }

        syncChild();
    };

    const init = () => {
        initSelect2();
        initFlatpickr();
        initInputRestrictions();
        // Gerekirse parent/child select referanslarini verip aktif et:
        // rebuildDependentSelect(
        //     document.getElementById('{{ParentSelectId}}'),
        //     document.getElementById('{{ChildSelectId}}')
        // );
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => {{ModuleName}}FormManager.init());
```
