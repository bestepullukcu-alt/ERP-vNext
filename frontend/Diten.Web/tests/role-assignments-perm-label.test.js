const { loadScript } = require("./load-script");

// FIX-RBAC-PERM-MODULE-ATTRIBUTION — the Role Permissions row label.
//
// The old screen rendered "{Verb} · {Entity}" once per permission: 415 rows, the verb leading every one of them,
// and the module name repeated inside a group card whose header already said it. PermLabel.split is the whole
// decision behind the new shape (source = row, verbs = chips) and is pure, so these tests pin it directly.
//
// They also pin the regression this change is most likely to cause: the codes must survive. Grouping, filtering
// and assignment travel on `entity.code` / `action.code`; only the labels are language.
describe("PermLabel.split (Role Permissions row labels)", () => {
    function load() {
        delete window.PermLabel;
        loadScript("wwwroot/assets/js/Governance/RoleAssignments/perm-label.js");
        return window.PermLabel;
    }

    const L10N = {
        ActionVerbs: { read: "Görüntüle", create: "Oluştur", manage: "Yönet", "bulk-delete": "Toplu Sil" },
        EntityNames: { "field-definitions": "Alan Tanımları", "work-report": "Çalışma Raporu" },
        ScopeLabels: { "tenant-wide": "Tüm kiracı" }
    };

    // The four real shapes the live catalog produces inside the "tasks" module.
    const perm = (resource, action) => ({ module: "tasks", resource, action, key: `platform.${resource}.${action}` });

    it("drops the module from the row — the group header already says it", () => {
        const out = load().split(perm("tasks", "read"), L10N);

        // Empty entity code is the signal "this row IS the module"; the caller supplies the module's own name.
        expect(out.entity.code).toBe("");
        expect(out.entity.label).toBe("");
        expect(out.action.label).toBe("Görüntüle");
    });

    it("keeps the sub-resource as the row and the verb as a chip", () => {
        const out = load().split(perm("tasks.field-definitions", "manage"), L10N);

        expect(out.entity.code).toBe("field-definitions");
        expect(out.entity.label).toBe("Alan Tanımları");
        expect(out.action.code).toBe("manage");
        expect(out.action.label).toBe("Yönet");
        expect(out.scope).toBeNull();
    });

    it("splits a reach suffix off the verb into its own scope chip", () => {
        const out = load().split(perm("tasks.work-report", "read-tenant-wide"), L10N);

        // The dangerous half is the reach, so it must not hide inside a 98th verb spelling.
        expect(out.action.code).toBe("read");
        expect(out.action.label).toBe("Görüntüle");
        expect(out.scope.code).toBe("tenant-wide");
        expect(out.scope.label).toBe("Tüm kiracı");
    });

    it("never emits the old '{Verb} · {Entity}' single string", () => {
        const PL = load();
        const out = PL.split(perm("tasks.work-report", "read"), L10N);

        // The regression guard for the shape itself: entity and action are SEPARATE fields, so a caller cannot
        // fall back to concatenating them without deleting the row/chip layout that depends on them.
        expect(typeof out.entity).toBe("object");
        expect(typeof out.action).toBe("object");
        expect(out.entity.label).toBe("Çalışma Raporu");
        expect(out.action.label).toBe("Görüntüle");
        expect(JSON.stringify(out)).not.toContain(" · ");
    });

    it("humanizes an unknown code instead of printing a raw slug — no fixed product list", () => {
        const PL = load();
        const out = PL.split({ module: "brand-new", resource: "brand-new.some-thing", action: "do-it" }, L10N);

        expect(out.entity.label).toBe("Some Thing");
        expect(out.action.label).toBe("Do It");
        expect(out.action.tone).toBe("secondary"); // unknown verbs are neutral, never uncoloured
    });

    // The module names itself in three different shapes across the live catalog, and every one of them used to
    // land in the row as a repeat of the group header the row already sits under.
    it.each([
        ["the same word",   { module: "tasks", resource: "tasks", action: "read" }],
        ["a longer form",   { module: "organization", resource: "organization-units", action: "read" }],
        ["a shorter form",  { module: "crm-account", resource: "account", action: "read" }],
        ["at the END of the path", { module: "work-report", resource: "tasks.work-report", action: "read" }]
    ])("treats the module written as %s as the row's own identity, not a sub-source", (_label, perm) => {
        expect(load().split(perm, L10N).entity.code).toBe("");
    });

    it("keeps a resource that merely shares a word with the module", () => {
        const PL = load();

        // "account-contact" inside "crm-contact" is a real, separate source — over-eager stripping would merge
        // two different things into one row.
        expect(PL.split({ module: "crm-contact", resource: "account-contact", action: "read" }, L10N).entity.code)
            .toBe("account-contact");
        // Genuine depth survives too: "knowledge.concept" in the crm module is not the module restating itself.
        expect(PL.split({ module: "crm", resource: "knowledge.concept", action: "read" }, L10N).entity.code)
            .toBe("knowledge.concept");
    });

    it("is DOM-free", () => {
        const PL = load();
        expect(() => PL.split(perm("tasks", "read"), null)).not.toThrow();
        expect(PL.split(perm("tasks", "read"), null).action.label).toBe("Read");
    });
});
