# GOLDEN RULE: Details Page Template (Read-Only)

Bu şablon, Diten ERP vNext projelerindeki tüm standart **Details (salt-okunur)** sayfaları için zorunlu iskelettir.

> ⚠️ **MANDATES**
> - `Layout = "_LayoutBackbone";` zorunludur.
> - Kart ızgarası `row g-6` olmalıdır.
> - Detay kartları içinde veri sunumu `dl.row` + `dt/dd` ile yapılır (yan yana grid ile bölme yapılmaz).
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
    Layout = "_LayoutBackbone";
}

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-6 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h4 class="mb-1">@(Model.Name ?? Model.Title ?? "-")</h4>
        <p class="mb-0">@Localizer["PageDescription"]</p>

        <nav aria-label="breadcrumb" class="mt-2 text-muted">
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

<div class="row g-6">
    {{!-- 3'lü kart düzeni (VIEW-002) --}}
    <div class="col-12 col-md-6 col-lg-4">
        <div class="card h-100">
            <div class="card-header border-bottom">
                <h5 class="card-title mb-0 d-flex align-items-center">
                    <i class="bx {{CardIcon1}} me-2"></i>@Localizer["CardTitle1"]
                </h5>
            </div>
            <div class="card-body mt-4">
                <dl class="row mb-0">
                    <dt class="col-12 fw-medium text-heading mb-1">@Localizer["FieldLabel1"]:</dt>
                    <dd class="col-12 mb-4">@(Model.Field1 ?? "-")</dd>
                </dl>
            </div>
        </div>
    </div>

    {{!-- Diğer kartlar buraya... --}}
</div>
```
