const { loadScript } = require("./load-script");

// FE-C 3/3 (MOD-0018-FU9) — user-role assignment row state. Assigned → removable (with permission);
// unassigned → assignable (with permission). UX-only logic.
describe("UserRoleAssignState.evaluate", () => {
    function load() {
        delete window.UserRoleAssignState;
        loadScript("wwwroot/assets/js/Governance/UserRoleAssignments/role-assign-state.js");
        return window.UserRoleAssignState;
    }

    const role = { id: "r1", name: "Admin" };

    it("assigned role is removable with the assign-role right", () => {
        const s = load().evaluate(role, true, true);
        expect(s).toEqual({ assigned: true, removable: true, assignable: false });
    });

    it("assigned role is NOT removable without the right", () => {
        const s = load().evaluate(role, true, false);
        expect(s.removable).toBe(false);
    });

    it("unassigned role is assignable with the right", () => {
        const s = load().evaluate(role, false, true);
        expect(s).toEqual({ assigned: false, removable: false, assignable: true });
    });

    it("unassigned role is NOT assignable without the right", () => {
        const s = load().evaluate(role, false, false);
        expect(s.assignable).toBe(false);
    });
});
