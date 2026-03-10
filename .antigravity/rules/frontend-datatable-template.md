# GOLDEN RULE: Standard DataTable UI Blueprint

Bu şablon, Diten ERP vNext projelerindeki tüm standart "Liste/CRUD" sayfaları (Örn: Countries, Cities, Currencies) için ZORUNLU HTML/Razor şablonudur.
Ajan, yeni bir modül sayfası oluştururken bu yapıyı BİREBİR kopyalayacak, sadece `{{DegiskenAdlari}}` kısımlarını ilgili modüle göre dolduracaktır. Özel panolar (Dashboard, Metric Cards) için bu şablonu kullanmayın.

## Master HTML Template

```html
@model IEnumerable<Diten.Web.Models.{{ModelName}}ViewModel>
@using Diten.Web.Views.{{AreaName}}
@using Microsoft.AspNetCore.Mvc.Localization
@inject IHtmlLocalizer<{{ModuleName}}> Localizer
@inject IHtmlLocalizer<Diten.Web.SharedResource> SharedLocalizer
@{
    ViewData["Title"] = Localizer["{{ModuleName}}Title"].Value;
    Layout = "_LayoutBackbone";
}

<partial name="_Filter" />

<div class="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-6 row-gap-4">
    <div class="d-flex flex-column justify-content-center">
        <h4 class="mb-1">@Localizer["{{ModuleName}}Title"]</h4>
        <p class="mb-0">@Localizer["PageDescription"]</p>
    </div>
</div>

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

<div class="card">
    <div id="skeleton-loader" class="p-4" style="display: none;">
        <div class="shimmer skeleton-row" style="width: 100%; height: 32px; margin-bottom: 2rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px; margin-bottom: 1rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px; margin-bottom: 1rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px; margin-bottom: 1rem;"></div>
        <div class="shimmer skeleton-row" style="width: 100%; height: 24px;"></div>
    </div>
    
    <div class="card-datatable table-responsive">
        <table class="datatables-{{ModuleNameLower}} table border-top">
            <thead>
                <tr>
                    <th></th>
                    <th class="cell-fit"><input type="checkbox" class="dt-checkboxes-select-all form-check-input"></th>
                    {{TableHeaders}}
                    <th>@SharedLocalizer["Status"]</th>
                    <th class="cell-fit">@SharedLocalizer["Actions"]</th>
                </tr>
            </thead>
        </table>
    </div>
</div>

<div class="offcanvas offcanvas-end" tabindex="-1" id="offcanvasDetailsPreview" aria-labelledby="offcanvasDetailsPreviewLabel" style="width: 480px;">
    <div class="offcanvas-header border-bottom">
        <div class="d-flex align-items-center">
            <div class="avatar avatar-md bg-primary-subtle text-primary rounded-circle d-flex align-items-center justify-content-center me-3">
                <i class="bx bx-info-circle fs-4"></i>
            </div>
            <div>
                <h5 id="oc-title" class="offcanvas-title mb-0">-</h5>
                <small id="oc-subtitle" class="text-muted">-</small>
            </div>
        </div>
        <button type="button" class="btn-close text-reset" data-bs-dismiss="offcanvas" aria-label="Close"></button>
    </div>

    <div class="offcanvas-body flex-grow-1 p-0">
        <div class="p-4">
            <div class="bg-label-secondary rounded p-3 mb-4 d-flex align-items-center justify-content-between">
                <div>
                    <span class="d-block text-muted small fw-medium mb-1">Status</span>
                    <span id="oc-status" class="badge bg-label-secondary">-</span>
                </div>
            </div>

            {{OffcanvasDetails}}
            </div>
    </div>
    <div class="offcanvas-footer border-top p-4 d-flex justify-content-between">
        <button type="button" class="btn btn-label-secondary w-50 me-2" data-bs-dismiss="offcanvas">@SharedLocalizer["Cancel"]</button>
        <a id="oc-btn-edit" href="#" class="btn btn-primary w-50">@SharedLocalizer["EditBtn"]</a>
    </div>
</div>

@section Scripts {
    <script>
        // Global Localization Variables
        window.L10n = window.L10n || {};
        window.L10n.Active = @Json.Serialize(SharedLocalizer["Active"].Value);
        window.L10n.Passive = @Json.Serialize(SharedLocalizer["Passive"].Value);
        window.L10n.Actions = @Json.Serialize(SharedLocalizer["Actions"].Value);
        window.L10n.Edit = @Json.Serialize(SharedLocalizer["EditBtn"].Value);
        window.L10n.ViewDetails = @Json.Serialize(SharedLocalizer["ViewDetails"].Value);
        window.L10n.Search = @Json.Serialize(SharedLocalizer["Search"].Value);
        window.L10n.Export = @Json.Serialize(SharedLocalizer["Export"].Value);
        window.L10n.AddNew{{ModuleName}} = @Json.Serialize(Localizer["AddNew{{ModuleName}}"].Value);
        window.L10n.DtNoRecords = @Json.Serialize(SharedLocalizer["DtNoRecords"].Value);
        window.L10n.DtInfo = @Json.Serialize(SharedLocalizer["DtInfo"].Value);
        window.L10n.DtInfoEmpty = @Json.Serialize(SharedLocalizer["DtInfoEmpty"].Value);
        window.L10n.BulkDelete = @Json.Serialize(SharedLocalizer["BulkDelete"].Value);
        window.L10n.BulkDeleteConfirm = @Json.Serialize(SharedLocalizer["BulkDeleteConfirm"].Value);
        window.L10n.AreYouSure = @Json.Serialize(SharedLocalizer["AreYouSure"].Value);
        window.L10n.Cancel = @Json.Serialize(SharedLocalizer["Cancel"].Value);
        
        // DİNAMİK L10N DEĞİŞKENLERİ BURAYA GELECEK
        {{DynamicL10nScripts}}
    </script>
    <script>
        function populateOffcanvas(element) {
            const dataStr = element.getAttribute('data-json').replace(/&#39;/g, "'");
            let data = {};
            try { data = JSON.parse(dataStr); } catch (e) { console.error("Could not parse row data", e); }

            document.getElementById('oc-title').innerText = data.name || data.title || '-';
            
            const statusEl = document.getElementById('oc-status');
            if (data.isActive) {
                statusEl.className = 'badge bg-label-success';
                statusEl.innerText = window.L10n.Active || 'Active';
            } else {
                statusEl.className = 'badge bg-label-secondary';
                statusEl.innerText = window.L10n.Passive || 'Passive';
            }

            document.getElementById('oc-btn-edit').href = `/{{ModuleName}}/Edit/${data.id}`;
            
            // DİNAMİK OFFCANVAS JS ATAMALARI BURAYA GELECEK
            {{DynamicOffcanvasJs}}
        }
    </script>
    <script src="~/assets/js/{{AreaName}}/{{ModuleName}}/index.js" asp-append-version="true"></script>
}