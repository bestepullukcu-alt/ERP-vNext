const { loadScript } = require("./load-script");

// FIX-ROLEPERMS-MODULE-LABEL — the Role Permissions screen must never print the raw module slug
// ("work-aggregation", "product-item-sku-master", "test-beta-mod") in its group headers or module filter.
// resolveModuleLabel is the whole label decision and is pure/DOM-free, so this locks the behaviour directly:
//   • a resolved name when the map knows the code (in ANY casing/separator spelling),
//   • a humanized code when it does NOT (nav endpoint 403/down, or a module with no resx key),
//   • and in NEITHER case a raw slug.
// It also pins the regression that this change is most likely to cause: the label is display-only — grouping,
// filtering and the permission key still travel on the CODE.
describe("ModuleLabel.resolve (Role Permissions module names)", () => {
    function load() {
        delete window.ModuleLabel;
        loadScript("wwwroot/assets/js/Governance/RoleAssignments/module-label.js");
        return window.ModuleLabel;
    }

    // The real shape: resx keys are canonical (uppercase, alphanumerics only) — see NavNameLocalizer.Normalize.
    const RESX = {
        WORKAGGREGATION: "Görev Merkezi",
        PRODUCTITEMSKUMASTER: "Product / Item / SKU Master",
        TASKS: "Görevler"
    };

    // Slugs the tenant-assignable permission catalog actually emits.
    const RAW_SLUGS = ["work-aggregation", "product-item-sku-master", "tasks", "test-beta-mod"];

    it("resolves a known code to its friendly name — the raw slug never reaches the screen", () => {
        const ML = load();
        const map = ML.buildMap(RESX, []);
        expect(ML.resolve("work-aggregation", map)).toBe("Görev Merkezi");
        expect(ML.resolve("product-item-sku-master", map)).toBe("Product / Item / SKU Master");
    });

    it("matches the permission slug to the nav code through ONE normalize (casing/separator drift)", () => {
        const ML = load();
        const map = ML.buildMap(RESX, []);
        ["work-aggregation", "WorkAggregation", "WORK-AGGREGATION", "work aggregation"].forEach((spelling) => {
            expect(ML.resolve(spelling, map)).toBe("Görev Merkezi");
        });
    });

    it("falls back to a humanized code when the map is EMPTY (nav 403 / endpoint down) — still no raw slug", () => {
        const ML = load();
        const emptyMap = ML.buildMap({}, []); // nothing localized, nothing from nav
        expect(ML.resolve("work-aggregation", emptyMap)).toBe("Work Aggregation");
        expect(ML.resolve("test-beta-mod", emptyMap)).toBe("Test Beta Mod");

        RAW_SLUGS.forEach((slug) => {
            expect(ML.resolve(slug, emptyMap)).not.toBe(slug);
        });
    });

    it("humanizes a code the map does not know, even when the map is populated", () => {
        const ML = load();
        const map = ML.buildMap(RESX, []);
        expect(ML.resolve("test-beta-mod", map)).toBe("Test Beta Mod");
    });

    it("NO raw slug survives resolution, with or without a map", () => {
        const ML = load();
        [ML.buildMap(RESX, []), ML.buildMap({}, [])].forEach((map) => {
            RAW_SLUGS.forEach((slug) => {
                const label = ML.resolve(slug, map);
                expect(label).toBeTruthy();
                expect(label).not.toBe(slug);
                expect(label).not.toMatch(/-/);        // slug separator gone
                expect(label).toMatch(/^[^a-z]/);      // never starts lowercase like a slug does
            });
        });
    });

    it("applies NavNameLocalizer precedence: tenant override > resx > nav default", () => {
        const ML = load();
        const nav = [
            { moduleCode: "WorkAggregation", moduleDisplayName: "Work Aggregation", moduleDisplayNameIsOverride: false },
            { moduleCode: "Tasks", moduleDisplayName: "Benim Görevlerim", moduleDisplayNameIsOverride: true },
            { moduleCode: "test-beta-mod", moduleDisplayName: "Beta Sandbox", moduleDisplayNameIsOverride: false }
        ];
        const map = ML.buildMap(RESX, nav);

        expect(ML.resolve("work-aggregation", map)).toBe("Görev Merkezi");   // resx beats the nav default
        expect(ML.resolve("tasks", map)).toBe("Benim Görevlerim");           // override beats the resx, as-typed
        expect(ML.resolve("test-beta-mod", map)).toBe("Beta Sandbox");       // nav default beats humanize
    });

    // ── REGRESSION LOCK ────────────────────────────────────────────────────────────────────────────
    // The screen groups, filters and searches on the module CODE; only the visible text was localized.
    // If a future change starts keying any of those on the label, these fail.
    it("normalize (the grouping/filter key) is code-derived and label-independent", () => {
        const ML = load();
        expect(ML.normalize("work-aggregation")).toBe("WORKAGGREGATION");
        expect(ML.normalize("WorkAggregation")).toBe("WORKAGGREGATION");
        // The label is NOT a key: localizing it must never change what the filter matches on.
        expect(ML.normalize("work-aggregation")).not.toBe(ML.normalize("Görev Merkezi"));
    });

    it("an empty/absent code yields an empty label, never 'Undefined'", () => {
        const ML = load();
        const map = ML.buildMap(RESX, []);
        expect(ML.resolve("", map)).toBe("");
        expect(ML.resolve(null, map)).toBe("");
        expect(ML.resolve(undefined, map)).toBe("");
    });

    it("honours an injected humanize (the screen reuses its own key-derived humanizer)", () => {
        const ML = load();
        const map = ML.buildMap({}, []);
        expect(ML.resolve("test-beta-mod", map, () => "INJECTED")).toBe("INJECTED");
    });
});
