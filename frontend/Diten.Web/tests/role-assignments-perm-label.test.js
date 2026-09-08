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

    // FIX-ROLEPERMS-ACTION-FAMILIES — tone/icon by family, matched on the action's WORDS.
    //
    // The exact-match table this replaces knew 13 of the catalog's 97 verbs, so 84 of them — 135 permissions —
    // rendered as the same grey cog. These tests pin the part that makes the family approach work at all: the
    // match is per WORD, so a qualified verb still lands in its family.
    describe("action families (tone + icon)", () => {
        it.each([
            ["read", "info"],
            ["create", "primary"],
            ["manage", "warning"],
            ["delete", "danger"],
            ["approve", "success"],
            ["cancel", "secondary"],
            ["assign", "dark"]
        ])("puts a plain %s in the %s family", (action, tone) => {
            expect(load().split({ module: "m", resource: "r", action }, L10N).action.tone).toBe(tone);
        });

        it.each([
            ["assign-role", "dark"],        // the case an exact-match list missed: assign + a qualifier
            ["assign-roles", "dark"],
            ["assign-permission", "dark"],
            ["bulk-delete", "danger"],      // qualifier FIRST — the verb still classifies
            ["view_status_history", "info"],
            ["audit.view", "info"],
            ["redact-actor", "danger"],
            ["approve-extension", "success"]
        ])("classifies the qualified verb %s as %s", (action, tone) => {
            expect(load().split({ module: "m", resource: "r", action }, L10N).action.tone).toBe(tone);
        });

        it("gives an unfamiliar verb a neutral tone AND the cog — never a missing icon", () => {
            const out = load().split({ module: "m", resource: "r", action: "temporary-issue" }, L10N);

            expect(out.action.tone).toBe("secondary");
            expect(out.action.icon).toBe("bx-cog");
            // Neutral is acceptable; unreadable is not — the label still comes out as words.
            expect(out.action.label).toBe("Temporary Issue");
        });

        it("keeps the glyphs export/import already had — a refactor may not take something away", () => {
            const PL = load();
            const ex = PL.split({ module: "m", resource: "r", action: "export" }, L10N);

            // No family fits "move data across the boundary", so the TONE is correctly neutral...
            expect(ex.action.tone).toBe("secondary");
            // ...but the glyph it carried before this change is not collateral damage.
            expect(ex.action.icon).toBe("bx-export");
            expect(PL.split({ module: "m", resource: "r", action: "import" }, L10N).action.icon).toBe("bx-import");
        });

        it("gives every family its own icon, so a coloured chip is never ambiguous", () => {
            const PL = load();
            const icons = PL.ACTION_FAMILIES.map((f) => f.icon).concat(PL.UNFAMILIAR.icon);

            expect(new Set(icons).size).toBe(icons.length);
        });
    });

    // İŞ 4 — display normalization. The catalog stores the same verb in four spellings.
    describe("action normalization (display only)", () => {
        it.each([
            ["Read", "read"],
            ["Create", "create"],
            ["Approve", "approve"],
            ["view_sensitive", "view-sensitive"],
            ["lookup_validation", "lookup-validation"]
        ])("folds %s to %s", (raw, expected) => {
            expect(load().split({ module: "m", resource: "r", action: raw }, L10N).action.code).toBe(expected);
        });

        it("makes the PascalCase seed read and colour like its kebab-case sibling", () => {
            const PL = load();
            const upper = PL.split({ module: "reference-data", resource: "businessreferencedata", action: "Read" }, L10N);
            const lower = PL.split({ module: "tasks", resource: "tasks", action: "read" }, L10N);

            // Same verb, same word, same colour — the BRD seed no longer prints "Read" beside a sibling's "Görüntüle".
            expect(upper.action.code).toBe(lower.action.code);
            expect(upper.action.label).toBe(lower.action.label);
            expect(upper.action.tone).toBe(lower.action.tone);
        });

        it("does NOT touch the permission key — ADR-001 §1 froze it", () => {
            const PL = load();
            const perm = { module: "reference-data", resource: "businessreferencedata", action: "Read", key: "platform.businessreferencedata.read" };

            // split() reads the key and never rewrites it; the caller posts perm.key, not action.code.
            expect(PL.split(perm, L10N).action.code).toBe("read");
            expect(perm.action).toBe("Read");
            expect(perm.key).toBe("platform.businessreferencedata.read");
        });
    });

    it("is DOM-free", () => {
        const PL = load();
        expect(() => PL.split(perm("tasks", "read"), null)).not.toThrow();
        expect(PL.split(perm("tasks", "read"), null).action.label).toBe("Read");
    });
});

/*
 * The two lists that must not drift apart.
 *
 * _IndexL10n.cshtml curates the verbs worth translating in seven languages; ACTION_FAMILIES decides the tone and
 * glyph of a chip. The first delivery grew one without the other: six of the nine newly translated verbs still
 * resolved to no family, so they read as proper words and were drawn as the generic cog -- half-finished in a way
 * no single test caught, because each list was individually correct.
 *
 * This reads the PRODUCTION view, not a copy of its list. Adding a verb to the bridge without giving it a family
 * fails here, naming the verb.
 */
describe("curated verbs and action families stay in step", () => {
    const fs = require("fs");
    const path = require("path");

    it("every verb in the l10n bridge resolves to a family", () => {
        delete window.PermLabel;
        loadScript("wwwroot/assets/js/Governance/RoleAssignments/perm-label.js");
        const P = window.PermLabel;

        const view = fs.readFileSync(
            path.join(__dirname, "..", "Views/Governance/RoleAssignments/_IndexL10n.cshtml"),
            "utf8"
        );
        // The ActionVerbs block only: ["verb"] = Localizer["ActionVerb_X"] entries.
        const verbs = [...view.matchAll(/\["([a-z][a-z0-9-]*)"\]\s*=\s*Localizer\["ActionVerb_/g)].map((m) => m[1]);

        expect(verbs.length).toBeGreaterThan(20);   // the bridge is really being read

        const orphans = verbs.filter((v) => {
            const family = P.resolveFamily(P.normalizeAction(v));
            return !family || family === P.UNFAMILIAR;
        });

        expect(orphans).toEqual([]);
    });
});
