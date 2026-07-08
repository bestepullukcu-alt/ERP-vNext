const { loadScript } = require("./load-script");

describe("HCM employee draft wizard", () => {
    function load() {
        document.body.innerHTML = `
            <div id="hcm-employee-draft-page" data-can-create-draft="true" data-api-base="/HCM/Employees/drafts/api">
                <button id="hcm-start-draft"></button>
                <button id="hcm-reload-draft"></button>
                <button id="hcm-save-draft"></button>
                <button id="hcm-validate-references"></button>
                <button id="hcm-review-draft"></button>
                <div id="hcm-error-state" class="d-none"></div>
                <div id="hcm-success-state" class="d-none"></div>
                <div id="hcm-permission-state" class="d-none"></div>
                <div id="hcm-reference-results"></div>
                <div id="hcm-review-blockers" class="d-none"></div>
                <select id="hcm-person-id"><option value="person-123" selected>Person</option></select>
                <input id="hcm-legal-name" value="Example Person" />
                <select id="hcm-sensitivity-level"><option value="standard" selected>Standard</option></select>
                <select id="hcm-worker-type"><option value="employee" selected>Employee</option></select>
                <select id="hcm-employment-type"><option value="full_time" selected>Full Time</option></select>
                <input id="hcm-hire-date" value="2026-06-18" />
                <input id="hcm-organization-unit-id" value="org-123" />
                <input id="hcm-position-id" value="position-123" />
                <input id="hcm-legal-entity-picker" />
                <input id="hcm-legal-entity-id" value="legal-123" />
                <datalist id="hcm-legal-entity-options"></datalist>
                <small id="hcm-legal-entity-status"></small>
                <span id="hcm-state-session"></span>
                <span id="hcm-state-version"></span>
                <span id="hcm-state-etag"></span>
                <span id="hcm-state-review"></span>
            </div>`;
        window.PersonReferencePicker = { init: vi.fn() };
        loadScript("wwwroot/assets/js/HCM/Employees/create-draft.js");
        return window.HcmEmployeeDraftWizard._test;
    }

    afterEach(() => {
        delete window.HcmEmployeeDraftWizard;
        delete window.PersonReferencePicker;
        delete global.fetch;
        document.body.innerHTML = "";
    });

    it("collects only approved safe draft fields", () => {
        const helper = load();

        expect(helper.collectPayload()).toEqual({
            person_id: "person-123",
            legal_name: "Example Person",
            worker_type: "employee",
            employment_type: "full_time",
            hire_date: "2026-06-18",
            organization_unit_id: "org-123",
            position_id: "position-123",
            legal_entity_id: "legal-123",
            sensitivity_level: "standard"
        });
        expect("user_id" in helper.collectPayload()).toBe(false);
        expect("government_id" in helper.collectPayload()).toBe(false);
    });

    it("builds reference validation request with personId and no userId", () => {
        const helper = load();
        const request = helper.collectReferenceRequest();

        expect(request.personId).toBe("person-123");
        expect(request.organizationUnitId).toBe("org-123");
        expect(request.positionId).toBe("position-123");
        expect(request.legalEntityId).toBe("legal-123");
        expect(request.idempotencyKey).toMatch(/^validate-/);
        expect("userId" in request).toBe(false);
    });

    it("maps important proxy failures to accessible messages", () => {
        const helper = load();

        expect(helper.classifyStatus(403)).toBe("Permission denied.");
        expect(helper.classifyStatus(409)).toContain("Reload");
        expect(helper.classifyStatus(503)).toBe("A dependency is unavailable.");
    });

    it("stores System.Text.Json eTag from create response and sends it on save", async () => {
        load();
        const requests = [];
        global.fetch = vi.fn(async (url, options = {}) => {
            requests.push({ url, options });
            if (options.method === "POST") {
                return {
                    ok: true,
                    status: 201,
                    text: async () => JSON.stringify({
                        data: {
                            draftSessionId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                            draftSchemaVersion: "employee-create-wizard.v1",
                            currentStep: "draft-created",
                            stepStatuses: {},
                            validationSummary: { results: [], canReview: false },
                            version: 1,
                            eTag: "\"1\""
                        }
                    })
                };
            }

            return {
                ok: true,
                status: 200,
                text: async () => JSON.stringify({
                    data: {
                        draftSessionId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        draftSchemaVersion: "employee-create-wizard.v1",
                        currentStep: "employee_draft",
                        steps: {},
                        stepStatuses: {},
                        referenceValidationSummary: { results: [], canReview: false },
                        reviewState: "not_reviewed",
                        version: 2,
                        eTag: "\"2\""
                    }
                })
            };
        });

        document.getElementById("hcm-start-draft").click();
        await new Promise(resolve => setTimeout(resolve, 0));

        expect(document.getElementById("hcm-state-etag").textContent).toBe("\"1\"");

        document.getElementById("hcm-save-draft").click();
        await new Promise(resolve => setTimeout(resolve, 0));

        const patchRequest = requests.find(request => request.options.method === "PATCH");
        expect(patchRequest).toBeTruthy();
        expect(patchRequest.options.headers["If-Match"]).toBe("\"1\"");
        expect(document.getElementById("hcm-state-etag").textContent).toBe("\"2\"");
    });

    it("retains exact legal entity guid input when lookup validation fails", async () => {
        load();
        const fixtureId = "02510000-0000-0000-0000-000000000004";
        global.fetch = vi.fn(async () => ({
            ok: false,
            status: 401,
            statusText: "Unauthorized",
            text: async () => JSON.stringify({ message: "Unauthorized" })
        }));

        const picker = document.getElementById("hcm-legal-entity-picker");
        const hidden = document.getElementById("hcm-legal-entity-id");
        hidden.value = "";
        picker.value = fixtureId;
        picker.dispatchEvent(new Event("change"));
        await vi.waitFor(() => expect(global.fetch).toHaveBeenCalledTimes(1));
        await vi.waitFor(() => expect(document.getElementById("hcm-legal-entity-status").textContent).toBe("Unauthorized"));

        expect(hidden.value).toBe(fixtureId);
    });

    it("retains resolved legal entity selection when display label blurs", async () => {
        load();
        const fixtureId = "02510000-0000-0000-0000-000000000004";
        global.fetch = vi.fn(async () => ({
            ok: true,
            status: 200,
            text: async () => JSON.stringify({
                data: {
                    legalEntityId: fixtureId,
                    displayName: "MOD0251 Smoke Legal Entity",
                    lifecycleState: "ACTIVE",
                    referenceable: true
                }
            })
        }));

        const picker = document.getElementById("hcm-legal-entity-picker");
        const hidden = document.getElementById("hcm-legal-entity-id");
        hidden.value = "";
        picker.value = fixtureId;
        picker.dispatchEvent(new Event("change"));
        await vi.waitFor(() => expect(document.getElementById("hcm-legal-entity-status").textContent).toContain("Linked Legal Entity"));

        expect(picker.value).toBe("MOD0251 Smoke Legal Entity");
        expect(hidden.value).toBe(fixtureId);

        picker.dispatchEvent(new Event("blur"));
        await new Promise(resolve => setTimeout(resolve, 0));

        expect(hidden.value).toBe(fixtureId);
    });
});
