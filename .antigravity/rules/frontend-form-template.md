# GOLDEN RULE: Form Page Template (Create/Edit)

Bu şablon, Diten ERP vNext projelerindeki tüm standart **Create/Edit** form sayfaları için (Razor + Sneat PRO) zorunlu iskelettir.

> ⚠️ **MANDATES**
> - Shell tipine göre `Layout = "_LayoutPlatformAdmin";` veya `Layout = "_LayoutTenantShell";` zorunludur.
> - Form sayfalarında kart satırı `row g-4`, kart İÇİNDEKİ alan satırı `row g-3` boşluğunu kullanır.
>   ⚠️ Bu satır eskiden `row g-6` diyordu ve **ürünle çelişiyordu**: ölçüldü — `row g-4` 170 yerde,
>   `row g-3` 170 yerde, `row g-6` ise yalnız 4 yerde ve hiçbiri form değil (DocumentManagement'ın
>   Details sayfaları). İki altın referans da (GoldenReferenceCompact `_Form.cshtml`,
>   GoldenReferenceSlim `_CreateEditOffcanvas.cshtml`) g-4/g-3 kullanıyor. Kural ürüne uyduruldu,
>   ürün kurala değil — çünkü kuralı okuyup g-6 yazan tek bir form sayfası çıkmadı.
> - `col-lg-10 mx-auto` kullanılmaz. Kartlar `col-12` içinde tam genişlikte olmalıdır.
> - Görünen tüm metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden gelmelidir.
> - Form `novalidate` ile çalışır; validation feedback Bootstrap 5 `invalid-feedback` ile yapılır.
> - Create/Edit üst başlığı kompakt action-page standardında olmalıdır: wrapper `mb-3`, başlık `h5.mb-0`.
> - Create/Edit sayfalarında breadcrumb korunur; ancak varsayılan zincir `{{ModuleName}}Title > Current Action` olmalıdır. `Home` ve `Breadcrumb{{AreaName}}` breadcrumb'ı standart form şablonunda kullanılmaz.
> - Form sayfalarında liste ekranındaki `PageDescription` bloğu tekrar edilmez.
> - Bağımlı select (örn. `ProductType -> Category`) varsa child alan başlangıçta disabled olabilir; ancak parent seçimi sonrası child seçenekleri DOM'da yeniden oluşturulmalı, uygunsuz eski değer temizlenmeli ve select2 state'i yeniden senkronlanmalıdır. Uygunsuz seçenekleri dropdown içinde disabled/gri halde bırakmak standart dışıdır.
> - Razor tarafında boolean HTML attribute'ları için `disabled="False"` benzeri kullanım YASAKTIR. Attribute ya tamamen render edilir ya da hiç render edilmez.
> - **HER ALAN `.diten-field` ile sarılır ve bir ikon taşır.** İstisna yalnız ikidir ve ikisi de yapısaldır:
>   `form-check`/switch (metin girintisi olan bir kutu değil, kontrollü bir etiket) ve tenant tanımlı
>   configurable alanlar (değer tipi yazım anında bilinmiyor, seçilecek bir ikon yok).
> - İkon **her zaman kontrolden ÖNCE** ve **her zaman `aria-hidden="true"`** — etiket alanın adını zaten
>   söylüyor, glif onu tekrar okutmamalı.
> - `.diten-field` **yalnız ikonu ve kontrolü** sarar. `<label>` sarmalayıcının DIŞINDA ve ÜSTÜNDE;
>   `form-text` · `invalid-feedback` · `asp-validation-for` sarmalayıcıdan SONRA gelir.
> - `textarea` için ikon ek sınıf alır: `diten-field-icon--top` (38px'lik satıra göre ortalama bir
>   textarea'da glifi üçüncü satırın yanına park eder). select2 için ek sınıf YOK — aynı sarmalayıcı yeter.
> - Bir picker'ın (tarih) üstündeki ikon **AÇMAK ZORUNDA**. Bağlamayı kendin yazma:
>   `assets/js/shared/diten-datefield.js` her ikisini de sahiplenir. Ölü ikon, ölü butonun kardeşidir.

---

## Nereye bakılır — bu kuralın CANLI karşılığı

Bu dosya sözleşmeyi anlatır; sözleşmenin **çalışan iki kopyası** üründe durur ve şablonu
kopyalarken bakılacak yer onlardır:

| Yüzey | Dosya | Neyi gösterir |
|---|---|---|
| Tam sayfa Create/Edit | `Views/DevEnablement/GoldenReferenceCompact/_Form.cshtml` | 12 alan · 4 bölüm başlığı · select2 · textarea · iki flatpickr |
| Offcanvas Create/Edit | `Views/DevEnablement/GoldenReferenceSlim/_CreateEditOffcanvas.cshtml` | 5 alan · offcanvas select2 · başlıksız (offcanvas'ta bölüm başlığı YOK) |

**Alan → ikon eşlemesi burada YAŞAMAZ.** `frontend/Diten.Web/tests/diten-field-icons.test.js`
içindeki `ICON_MAP` sabitindedir; kural ("EVERY field carries an icon") aynı dosyada çivilenir.
Yeni bir alan eklerken glifi oraya yaz — böylece seçim gözden geçirilen bir karar olur, her ekranda
yeniden yazılan bir alışkanlık değil.

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
    Layout = "_LayoutTenantShell"; // or "_LayoutPlatformAdmin" if module belongs to admin shell
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

            <div class="row g-4">
                {{!-- Kartlar (field grouping) buraya. Örn:
                <div class="col-12 col-lg-6">
                    <section class="card h-100">
                        <div class="card-body p-4">
                            {{!-- BÖLÜM BAŞLIĞI — tek idiom. .card-section-title sınıfın kendisi
                                 uppercase + heading rengi + 600 ağırlığı taşır, glifi primary'ye
                                 boyar; ayrıca yardımcı sınıf dizmeye gerek yok. Açıklama satırı
                                 varsa başlık mb-1 alır ve altına .card-section-desc gelir, yoksa
                                 başlık mb-4 alır. --}}
                            <h6 class="card-section-title mb-4"><i class="bx {{CardIcon}}"></i>@Localizer["CardTitle"]</h6>

                            <div class="row g-3">
                                <div class="col-12">
                                    {{!-- label SARMALAYICININ DIŞINDA --}}
                                    <label asp-for="Name" class="form-label fw-medium">@Localizer["Name"] <span class="text-danger">*</span></label>
                                    {{!-- .diten-field yalnız İKON + KONTROL içerir --}}
                                    <div class="diten-field">
                                        <i class="bx {{FieldIcon}} diten-field-icon" aria-hidden="true"></i>
                                        <input asp-for="Name" class="form-control" required />
                                    </div>
                                    {{!-- geri bildirim sarmalayıcıdan SONRA --}}
                                    <span asp-validation-for="Name" class="invalid-feedback"></span>
                                </div>

                                <div class="col-12">
                                    <label asp-for="Description" class="form-label fw-medium">@SharedLocalizer["Description"]</label>
                                    <div class="diten-field">
                                        {{!-- textarea: --top varyantı, yoksa glif üçüncü satırın yanına düşer --}}
                                        <i class="bx bx-align-left diten-field-icon diten-field-icon--top" aria-hidden="true"></i>
                                        <textarea asp-for="Description" class="form-control" rows="4"></textarea>
                                    </div>
                                </div>

                                <div class="col-12">
                                    <label asp-for="Type" class="form-label fw-medium">@Localizer["Type"]</label>
                                    <div class="diten-field">
                                        {{!-- select2: aynı sarmalayıcı, EK SINIF YOK --}}
                                        <i class="bx {{FieldIcon}} diten-field-icon" aria-hidden="true"></i>
                                        <select asp-for="Type" class="select2 form-select">
                                            <option value="">@SharedLocalizer["SelectPlaceholder"]</option>
                                        </select>
                                    </div>
                                </div>

                                <div class="col-12">
                                    <label asp-for="EffectiveDate" class="form-label fw-medium">@Localizer["EffectiveDate"]</label>
                                    <div class="diten-field">
                                        {{!-- ikonun tıklaması DitenDateField tarafından takvime bağlanır --}}
                                        <i class="bx bx-calendar diten-field-icon" aria-hidden="true"></i>
                                        <input asp-for="EffectiveDate" type="text" class="form-control flatpickr-date" placeholder="YYYY-MM-DD" />
                                    </div>
                                </div>

                                <div class="col-12">
                                    {{!-- SWITCH: ikon YOK (istisna). Metin girintisi olan bir kutu değil. --}}
                                    <div class="form-check form-switch mt-2">
                                        <input type="hidden" name="IsActive" value="false" />
                                        <input asp-for="IsActive" class="form-check-input" role="switch" value="true" />
                                        <label asp-for="IsActive" class="form-check-label ms-2">@SharedLocalizer["Active"]</label>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </section>
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
    <script src="~/assets/js/shared/diten-datefield.js" asp-append-version="true"></script>
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

    /*
     * Tarih alanlari — PAYLASILAN bilesen uzerinden, burada YENIDEN YAZILMAZ.
     *
     * Yerinde flatpickr kurmak takvimi cizmeye yeter, ikonu CALISTIRMAYA yetmez: ikon alanin inline
     * basinda kontrolun USTUNDE durur, kullanicinin "takvim" diye nisan aldigi tiklama glife duser.
     * DitenDateField iki yarim da sahiplenir (bkz. assets/js/shared/diten-datefield.js).
     */
    const initFlatpickr = () => {
        if (!window.DitenDateField) return 0;
        return window.DitenDateField.enhance(document);
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
