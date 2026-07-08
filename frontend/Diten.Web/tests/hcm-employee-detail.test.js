const { loadScript } = require("./load-script");
const fs = require("fs");
const path = require("path");

describe("HCM employee detail", () => {
    function load(canView = "true") {
        document.body.innerHTML = `
            <div id="hcm-employee-detail-page"
                 data-api-base="/HCM/Employees/api"
                 data-employee-id="02510000-0000-0000-0000-000000000251"
                 data-can-view="${canView}">
                <div id="hcm-detail-loading"></div>
                <div id="hcm-detail-error" class="d-none"></div>
                <div id="hcm-detail-content" class="d-none">
                    <span data-field="employeeNumber"></span>
                    <span data-field="displayName"></span>
                    <span data-field="employeeStatus"></span>
                    <span data-field="sensitivityLevel"></span>
                    <span data-field="personId"></span>
                    <span data-field="version"></span>
                    <span data-field="etag"></span>
                    <span data-field="updatedAt"></span>
                    <span data-field="legalFirstName"></span>
                    <span data-field="legalMiddleName"></span>
                    <span data-field="legalLastName"></span>
                    <span data-field="preferredName"></span>
                    <span data-field="nationalityCode"></span>
                    <span data-field="workEmail"></span>
                    <span data-field="sensitiveFieldsMasked"></span>
                    <span data-field="governmentIdentifierPresent"></span>
                    <table><tbody id="hcm-employment-records-body"></tbody></table>
                    <div id="hcm-employment-records-empty"></div>
                </div>
            </div>`;
        window.L10n = {
            DependencyError: "Dependency unavailable.",
            EmptyValue: "",
            ErrorOccurred: "Request failed.",
            ForbiddenState: "Permission denied.",
            GovernmentIdentifierAbsent: "Not exposed",
            GovernmentIdentifierPresentMasked: "Masked",
            NotFoundState: "Employee not found.",
            SensitiveFieldsMasked: "Masked pending MOD-0314.",
            SensitiveFieldsSafeOnly: "Safe fields only."
        };
        loadScript("wwwroot/assets/js/HCM/Employees/details.js");
        return window.HcmEmployeeDetail._test;
    }

    afterEach(() => {
        delete window.HcmEmployeeDetail;
        delete window.L10n;
        delete window.fetch;
        document.body.innerHTML = "";
    });

    it("classifies authorization, missing rows, and dependency failures", () => {
        const helper = load();

        expect(helper.classifyStatus(401)).toBe("Permission denied.");
        expect(helper.classifyStatus(403)).toBe("Permission denied.");
        expect(helper.classifyStatus(404)).toBe("Employee not found.");
        expect(helper.classifyStatus(503)).toBe("Dependency unavailable.");
        expect(helper.classifyStatus(400)).toBe("Request failed.");
    });

    it("builds display name from safe legal profile fields", () => {
        const helper = load();

        expect(helper.buildDisplayName({
            legalProfile: {
                legalFirstName: "Ada",
                legalMiddleName: "Byron",
                legalLastName: "Lovelace"
            }
        })).toBe("Ada Byron Lovelace");

        expect(helper.buildDisplayName({
            legalProfile: {
                preferredName: "Amazing Grace",
                legalFirstName: "Grace",
                legalLastName: "Hopper"
            }
        })).toBe("Amazing Grace");
    });

    it("loads through the same-origin proxy and renders no edit or save controls", async () => {
        load();
        let requestedUrl = "";
        window.fetch = vi.fn(async (url) => {
            requestedUrl = url;
            return {
                ok: true,
                status: 200,
                text: async () => JSON.stringify({
                    data: {
                        employeeNumber: "E-001",
                        personId: "02510000-0000-0000-0000-000000000100",
                        legalProfile: {
                            legalFirstName: "Ada",
                            legalLastName: "Lovelace",
                            governmentIdentifierPresent: false
                        },
                        employmentRecords: [],
                        employeeStatus: "active",
                        sensitivityLevel: "standard",
                        sensitiveFieldsMasked: true,
                        version: 1,
                        etag: "\"1\"",
                        updatedAt: "2026-06-20T00:00:00Z"
                    }
                })
            };
        });

        document.dispatchEvent(new Event("DOMContentLoaded"));
        await new Promise((resolve) => setTimeout(resolve, 0));

        expect(requestedUrl).toBe("/HCM/Employees/api/02510000-0000-0000-0000-000000000251");
        expect(requestedUrl).not.toContain("5056");
        expect(requestedUrl).not.toContain("5057");
        expect(requestedUrl).not.toContain("5059");
        expect(requestedUrl).not.toContain("5060");
        expect(document.querySelector("form")).toBeNull();
        expect(document.querySelector("input")).toBeNull();
        expect(document.querySelector("button")).toBeNull();
        expect(document.body.textContent.toLowerCase()).not.toContain("save");
        expect(document.body.textContent.toLowerCase()).not.toContain("edit");
    });

    it("does not render future status evidence or audit placeholder partials", () => {
        const source = fs.readFileSync(
            path.join(__dirname, "../Views/HCM/Employees/Details.cshtml"),
            "utf8");

        expect(source).not.toContain("_DetailPlaceholders");
        expect(source).not.toContain("StatusHistoryPlaceholder");
        expect(source).not.toContain("EvidencePlaceholder");
        expect(source).not.toContain("AuditPlaceholder");
    });
});
