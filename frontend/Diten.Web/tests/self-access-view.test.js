const { loadScript } = require("./load-script");

// FE-E (MOD-0018-FU9) — pure view-model builder for the FU14 self-explain DTO. Two separate
// observations, NO combined verdict, bounded to the DTO's redacted fields.
describe("SelfAccessView.build", () => {
    function load() {
        delete window.SelfAccessView;
        loadScript("wwwroot/assets/js/Platform/SelfAccess/self-access-view.js");
        return window.SelfAccessView;
    }

    const dto = {
        mode: "self",
        permissionSatisfied: true,
        requiredPermission: "platform.tenants.read",
        permissionMatch: "canonical",
        matchedViaLegacyAlias: false,
        actorType: "platform_admin",
        tenantId: "00000000-0000-0000-0000-000000000001",
        scopeKinds: ["OrgUnit"],
        scopeCounts: { OrgUnit: 3 },
        scopeNotes: ["data-scope is opt-in per resource"],
        tokenExpiresAtUtc: "2026-06-12T10:00:00Z",
        freshnessNotes: ["bounded note"],
        diagnosticFailure: false
    };

    it("produces two separate observations + meta", () => {
        const vm = load().build(dto);
        expect(vm.permission).toEqual({
            requiredPermission: "platform.tenants.read",
            satisfied: true,
            match: "canonical",
            matchedViaLegacyAlias: false
        });
        expect(vm.scope.kinds).toEqual(["OrgUnit"]);
        expect(vm.scope.counts).toEqual({ OrgUnit: 3 });
        expect(vm.meta.actorType).toBe("platform_admin");
    });

    it("never synthesises a combined 'allowed' verdict", () => {
        const vm = load().build(dto);
        expect("allowed" in vm).toBe(false);
        expect("allowed" in vm.permission).toBe(false);
        expect("allowed" in vm.scope).toBe(false);
        // top-level keys are exactly the two observations + meta
        expect(Object.keys(vm).sort()).toEqual(["meta", "permission", "scope"]);
    });

    it("diagnosticFailure marks scope unavailable but preserves the permission observation", () => {
        const vm = load().build(Object.assign({}, dto, { diagnosticFailure: true }));
        expect(vm.scope.unavailable).toBe(true);
        expect(vm.meta.diagnosticFailure).toBe(true);
        expect(vm.permission.satisfied).toBe(true); // permission observation preserved
    });

    it("is bounded: missing/garbage fields degrade safely", () => {
        const vm = load().build({});
        expect(vm.permission.match).toBe("missing");
        expect(vm.permission.satisfied).toBe(false);
        expect(vm.scope.kinds).toEqual([]);
        expect(vm.scope.counts).toEqual({});
        expect(vm.meta.mode).toBe("self");
    });

    it("returns null for a non-object dto", () => {
        const helper = load();
        expect(helper.build(null)).toBeNull();
        expect(helper.build("nope")).toBeNull();
    });
});
