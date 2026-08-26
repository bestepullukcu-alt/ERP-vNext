const fs = require("fs");
const path = require("path");

describe("MOD-0290 Global Product Register", () => {
    const read = (relativePath) => fs.readFileSync(path.join(__dirname, "..", relativePath), "utf8");
    const indexScript = () => read("wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js");
    const l10nScript = () => read("wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.l10n.js");
    const controller = () => read("Controllers/GlobalProductsController.cs");

    it("uses the server-verified create permission to render the canonical toolbar action", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/Index.cshtml");
        const source = indexScript();

        expect(view).toContain('@inject Diten.Web.Services.IPermissionSnapshot Permissions');
        expect(view).toContain('Permissions.Has("mdm.global-products.create")');
        expect(view).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
        expect(source).toContain("const canCreate = document.querySelector('[data-can-create]')");
        expect(source).toContain('exportButtons(canCreate ? L.AddNew : null');
        expect(l10nScript()).toContain('normalized[toPascalCase(key)] = raw[key]');
    });

    it("uses only the same-origin MVC proxy from browser code", () => {
        const source = indexScript();

        expect(source).toContain("const endpoint = '/MasterDataManagement/GlobalProducts/api'");
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['\"`]Bearer/);
    });

    it("keeps the browser create payload to the single user field", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml");
        const source = indexScript();

        expect(view.match(/name="GlobalProductName"/g)).toHaveLength(1);
        expect(view).not.toMatch(/name="(?:TenantId|CanonicalCode|ReservationId|IdempotencyKey)"/);
        expect(source).not.toMatch(/body\.(?:set|append)\(['\"](?:TenantId|CanonicalCode|ReservationId|IdempotencyKey)/);
    });

    it("orchestrates reservation and draft creation on the MVC server", () => {
        const source = controller();

        expect(source).toContain("ReserveCodeAsync(model.GlobalProductName");
        expect(source).toContain("CreateDraftAsync(");
        expect(source).toContain("/api/global-products/code-reservations");
        expect(source).toContain("/api/global-products/drafts");
        expect(source).toContain("Guid.NewGuid().ToString(\"N\")");
    });

    it("keeps list, detail, selector, and create under the canonical proxy route", () => {
        const source = controller();

        expect(source).toContain('[Route("MasterDataManagement/GlobalProducts")]');
        expect(source).toContain('[HttpGet("api")]');
        expect(source).toContain('[HttpGet("api/{id:guid}")]');
        expect(source).toContain('[HttpGet("api/selector")]');
        expect(source).toContain('[HttpPost("api")]');
    });

    it("offers only the read-only details row action", () => {
        const source = indexScript();

        expect(source).toContain("className: 'js-quick-view'");
        expect(source).not.toMatch(/delete-record|js-edit-item|\/bulk/);
    });

    it("provides the future ABB selector through the same proxy surface", () => {
        const source = indexScript();

        expect(source).toContain("window.DitenSelectors.globalProducts");
        expect(source).toContain("`${endpoint}/selector?");
    });
});
