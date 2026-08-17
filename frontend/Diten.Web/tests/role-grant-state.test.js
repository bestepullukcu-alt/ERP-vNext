const { loadScript } = require("./load-script");

// FE-C 2/3 (MOD-0018-FU9) — GrantSource-aware row state for role-permission assignment.
// System/Module are locked; only Manual is removable; unassigned is assignable. UX-only logic.
describe("RoleGrantState.evaluate", () => {
    function load() {
        delete window.RoleGrantState;
        loadScript("wwwroot/assets/js/Governance/RoleAssignments/grant-state.js");
        return window.RoleGrantState;
    }

    const perm = { id: "p1", key: "auth.users.read" };

    it("System (baseline) grant is locked, not removable, badge=Baseline", () => {
        const s = load().evaluate(perm, { grantSource: "System" }, true);
        expect(s.assigned).toBe(true);
        expect(s.source).toBe("System");
        expect(s.locked).toBe(true);
        expect(s.removable).toBe(false);
        expect(s.assignable).toBe(false);
        expect(s.badge).toEqual({ key: "Baseline" });
    });

    it("Module grant is locked, not removable, badge carries the source module code", () => {
        const s = load().evaluate(perm, { grantSource: "Module", sourceModuleCode: "mdm" }, true);
        expect(s.locked).toBe(true);
        expect(s.removable).toBe(false);
        expect(s.badge).toEqual({ key: "Module", moduleCode: "mdm" });
    });

    it("Manual grant is removable (with permission) and badge=Manual", () => {
        const s = load().evaluate(perm, { grantSource: "Manual" }, true);
        expect(s.locked).toBe(false);
        expect(s.removable).toBe(true);
        expect(s.assignable).toBe(false);
        expect(s.badge).toEqual({ key: "Manual" });
    });

    it("Manual grant is NOT removable without the assign-permission right", () => {
        const s = load().evaluate(perm, { grantSource: "Manual" }, false);
        expect(s.removable).toBe(false);
    });

    it("unassigned permission is assignable only with the right", () => {
        const helper = load();
        expect(helper.evaluate(perm, null, true)).toMatchObject({ assigned: false, assignable: true, badge: null });
        expect(helper.evaluate(perm, null, false)).toMatchObject({ assignable: false });
    });

    it("source matching is case-insensitive", () => {
        const s = load().evaluate(perm, { grantSource: "manual" }, true);
        expect(s.source).toBe("Manual");
        expect(s.removable).toBe(true);
    });
});
