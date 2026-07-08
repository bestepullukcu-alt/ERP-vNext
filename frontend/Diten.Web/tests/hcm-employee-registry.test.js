const { loadScript } = require("./load-script");
const fs = require("fs");
const path = require("path");

describe("HCM employee registry", () => {
    function load() {
        document.body.innerHTML = `
            <div id="hcm-employee-registry-page" data-api-base="/HCM/Employees/api" data-can-search="true" data-can-view="true">
                <div id="hcm-registry-error" class="d-none"></div>
                <form id="filterForm" data-no-tracker>
                    <select id="filterEmployeeStatus"><option value=""></option><option value="active" selected>Active</option></select>
                    <select id="filterWorkerType"><option value=""></option><option value="employee" selected>Employee</option></select>
                    <select id="filterEmploymentType"><option value=""></option><option value="full_time" selected>Full Time</option></select>
                    <input id="filterLegalEntityId" value="02510000-0000-0000-0000-000000000004" />
                </form>
                <table class="datatables-employees"></table>
            </div>`;
        window.L10n = {
            DependencyError: "Dependency unavailable.",
            ForbiddenState: "Permission denied.",
            ErrorOccurred: "Request failed."
        };
        loadScript("wwwroot/assets/js/HCM/Employees/index.js");
        return window.HcmEmployeeRegistry._test;
    }

    afterEach(() => {
        delete window.HcmEmployeeRegistry;
        delete window.L10n;
        document.body.innerHTML = "";
    });

    it("maps DataTables paging and sort to safe registry query params", () => {
        const helper = load();

        const parameters = helper.buildQueryParams({
            start: 40,
            length: 20,
            search: { value: "Ada" },
            order: [{ column: 3, dir: "asc" }],
            columns: [
                { name: "control" },
                { name: "checkbox" },
                { name: "employeeNumber" },
                { name: "displayName" }
            ]
        });

        expect(parameters).toEqual({
            search: "Ada",
            employeeStatus: "",
            workerType: "",
            employmentType: "",
            legalEntityId: "",
            page: 3,
            pageSize: 20,
            sortBy: "displayName",
            sortDirection: "asc"
        });
    });

    it("normalizes HCM response envelope for server-side DataTables", () => {
        const helper = load();

        const result = helper.normalizeResponse({
            data: {
                items: [{ employeeId: "employee-1" }],
                totalCount: 1
            }
        }, 7);

        expect(result).toEqual({
            draw: 7,
            recordsTotal: 1,
            recordsFiltered: 1,
            data: [{ employeeId: "employee-1" }]
        });
    });

    it("classifies authorization and dependency failures without exposing internals", () => {
        const helper = load();

        expect(helper.classifyStatus(401)).toBe("Permission denied.");
        expect(helper.classifyStatus(403)).toBe("Permission denied.");
        expect(helper.classifyStatus(503)).toBe("Dependency unavailable.");
        expect(helper.classifyStatus(400)).toBe("Request failed.");
    });

    it("builds same-origin query strings and does not introduce service ports", () => {
        const helper = load();
        const query = helper.toQueryString({
            search: "Ada",
            employeeStatus: "",
            page: 1,
            pageSize: 20
        });

        expect(query).toBe("?search=Ada&page=1&pageSize=20");
        expect(query).not.toContain("5056");
        expect(query).not.toContain("5057");
        expect(query).not.toContain("5059");
        expect(query).not.toContain("5060");
    });

    it("does not configure governed export buttons in the current P2 scope", () => {
        const source = fs.readFileSync(
            path.join(__dirname, "../wwwroot/assets/js/HCM/Employees/index.js"),
            "utf8");

        expect(source).not.toContain("DtDefaults.exportButtons");
        expect(source).not.toContain("exportColumns");
    });
});
