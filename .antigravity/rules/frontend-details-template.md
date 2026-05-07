# GOLDEN RULE: Details Page Template (Read-Only)

Bu şablon, Diten ERP vNext projelerindeki tüm standart **Details (salt-okunur)** sayfaları için zorunlu iskelettir.

> ⚠️ **MANDATES**
> - Shell tipine göre `Layout = "_LayoutPlatformAdmin";` veya `Layout = "_LayoutTenantShell";` zorunludur.
> - Kart ızgarası `row g-6` olmalıdır.
> - Detay kartları içinde veri sunumu `dl.row` + `dt/dd` ile yapılır (yan yana grid ile bölme yapılmaz).
> - Details surface'leri liste/card yüzeyiyle aynı radius'u kullanmalıdır: `card backbone-preview-section` birlikte kullanılmalı, border kaldırma ihtiyacı varsa yalnız sayfa wrapper'ı altında scoped CSS ile `border: 0; border-radius: var(--bs-card-border-radius); background: var(--bs-card-bg);` uygulanmalıdır.
> - Details surface border/radius/background için hardcoded renk, px radius veya shell geneline yayılan global override kullanılmaz; Sneat/Bootstrap token'ları kullanılmalıdır.
> - Details içinde tab gerekiyorsa düz `nav-tabs` kullanılmaz. WorkCenter standardındaki card-header tab bar kullanılır: dış yüzey `card mb-4`, header `card-header p-3`, liste `nav nav-pills d-inline-flex gap-2 flex-wrap`, butonlar `nav-link small border shadow-none wc-tab-compact`, ikonlar `bx ... wc-tab-icon`. Desktop'ta ikon+metin, mobilde sadece ikon gösterilir.
> - Görünen tüm metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden gelmelidir.
> - Referans kurallar: `.antigravity/workflows/details-page-rules.md`

---

## Details.cshtml Şablonu

```cshtml
@model Diten.Web.Models.{{ModelName}}ViewModel
@using Diten.Web.Views.{{AreaName}}.{{ModuleName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}Index> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer
@{
    ViewData["Title"] = SharedLocalizer["Details"].Value + " - " + (Model.Name ?? Model.Title ?? "-");
    Layout = "_LayoutTenantShell"; // or "_LayoutPlatformAdmin" if module belongs to admin shell
}

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-4 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h4 class="mb-1">@(Model.Name ?? Model.Title ?? "-")</h4>

        <nav aria-label="breadcrumb" class="text-muted">
            <ol class="breadcrumb mb-0 py-0">
                <li class="breadcrumb-item"><a href="/">@Localizer["BreadcrumbHome"]</a></li>
                <li class="breadcrumb-item"><a href="javascript:void(0);">@Localizer["Breadcrumb{{AreaName}}"]</a></li>
                <li class="breadcrumb-item"><a asp-action="Index">@Localizer["{{ModuleName}}Title"]</a></li>
                <li class="breadcrumb-item active text-primary">@SharedLocalizer["Details"]</li>
            </ol>
        </nav>
    </div>

    <div class="d-flex align-content-center flex-wrap gap-4">
        <a asp-action="Index" class="btn btn-label-secondary border">@SharedLocalizer["BackToList"]</a>
        <a asp-action="Edit" asp-route-id="@Model.Id" class="btn btn-primary">@SharedLocalizer["EditRecord"]</a>
    </div>
</div>

{{!-- Opsiyonel: Details tab bar. Çoklu tab gerekiyorsa bu WorkCenter tarzı navbar kullanılır; düz nav-tabs kullanılmaz. --}}
<div class="card mb-4">
    <div class="card-header p-3">
        <div class="nav-align-top">
            <ul class="nav nav-pills d-inline-flex gap-2 flex-wrap" role="tablist">
                <li class="nav-item mb-1 mb-sm-0" role="presentation">
                    <button type="button"
                            class="nav-link active small border shadow-none wc-tab-compact"
                            id="{{ModuleNameLower}}-overview-tab-button"
                            data-bs-toggle="tab"
                            data-bs-target="#{{ModuleNameLower}}-overview-tab"
                            role="tab"
                            aria-controls="{{ModuleNameLower}}-overview-tab"
                            aria-selected="true">
                        <span class="d-none d-sm-inline-flex align-items-center">
                            <i class="bx bx-info-circle me-1 wc-tab-icon"></i>@Localizer["OverviewTab"]
                        </span>
                        <i class="bx bx-info-circle d-sm-none wc-tab-icon"></i>
                    </button>
                </li>
                {{!-- Additional tabs follow the same nav-link + icon pattern. --}}
            </ul>
        </div>
    </div>
</div>

<div class="row g-6">
    {{!-- 3'lü kart düzeni (VIEW-002) --}}
    <div class="col-12 col-md-6 col-lg-4">
        <section class="card backbone-preview-section h-100 p-4">
            <h5 class="card-title mb-4 d-flex align-items-center">
                <i class="bx {{CardIcon1}} me-2"></i>@Localizer["CardTitle1"]
            </h5>
            <dl class="row mb-0">
                <dt class="col-12 fw-medium text-heading mb-1">@Localizer["FieldLabel1"]:</dt>
                <dd class="col-12 mb-4">@(Model.Field1 ?? "-")</dd>
            </dl>
        </section>
    </div>

    {{!-- Diğer kartlar buraya... --}}
    
    {{!-- Versiyon Geçmişi Sütunu (Opsiyonel) --}}
    <div class="col-12 col-md-4">
        <section class="card backbone-preview-section h-100 p-4">
            <h5 class="card-title mb-4 d-flex align-items-center">
                <i class="bx bx-history me-2"></i>@Localizer["RevisionHistory"]
            </h5>
            <div class="list-group list-group-flush">
                @* Versiyon listesi döngüsü *@
            </div>
        </section>
    </div>
</div>
```
