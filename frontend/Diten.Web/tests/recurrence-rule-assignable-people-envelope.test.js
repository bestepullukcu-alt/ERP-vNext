const { loadScript } = require("./load-script");

/*
 * BL-350 — the recurrence-rule form's person picker was always empty.
 *
 * fetchJson() unwrapped `payload.data` and returned it only `Array.isArray(rows)` was true, otherwise `[]`.
 * That is correct for /assignable-positions and /task-templates, which DO answer a bare array — but
 * /assignable-people answers AssignablePersonLookupDto { people, excluded } (BL-057: only the server knows WHY
 * the rest are missing), so on the wire it is `{ data: { people: [...], excluded: {...} } }`. `payload.data` is
 * an OBJECT there, `Array.isArray` is false, and the picker silently got `[]` forever — indistinguishable from
 * "nobody is assignable".
 *
 * Tasks/api.js already documents and unwraps this exact envelope in its own `assignablePeople()` (the comment
 * there calls it "THE ONLY PLACE THE `{ people, excluded }` SHAPE IS UNWRAPPED" — this form predates that
 * lesson and re-implements its own fetch instead of reusing it, which is how it missed the shape).
 */
describe("RecurrenceRules form: assignable-people envelope (BL-350)", () => {
    const PERSON_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    const POSITION_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    const TEMPLATE_ID = "cccccccc-cccc-cccc-cccc-cccccccccccc";

    const markup = () => `
        <select data-recurrence-target>
            <option value="Person">Person</option>
            <option value="PositionPool">Pool</option>
        </select>
        <div data-recurrence-person>
            <select data-recurrence-assignee></select>
        </div>
        <div class="d-none" data-recurrence-pool>
            <select data-recurrence-position></select>
        </div>
        <select data-recurrence-template>
            <option value="">None</option>
        </select>`;

    // Real shapes: assignable-people is the { people, excluded } envelope; the other two are bare arrays under
    // `data`, exactly as the task description says they must stay.
    const mockFetch = () => vi.fn(async (url) => {
        if (url.includes("assignable-people")) {
            return {
                ok: true,
                json: async () => ({
                    data: {
                        people: [{
                            userId: PERSON_ID,
                            displayName: "Selin Aras",
                            positionName: "QA Specialist",
                            organizationUnitName: "Facility A"
                        }],
                        excluded: { total: 0, notInScope: 0, positionNotActive: 0, outOfScope: 0 }
                    }
                })
            };
        }
        if (url.includes("assignable-positions")) {
            return {
                ok: true,
                json: async () => ({
                    data: [{
                        positionId: POSITION_ID,
                        positionName: "QA Specialist",
                        organizationUnitName: "Facility A"
                    }]
                })
            };
        }
        if (url.includes("task-templates")) {
            return { ok: true, json: async () => ({ data: [{ id: TEMPLATE_ID, name: "Monthly QA Review" }] }) };
        }
        throw new Error(`Unexpected fetch in test: ${url}`);
    });

    // Loaded ONCE: the module is a bare IIFE that registers a single DOMContentLoaded listener with no exported
    // surface, so each test re-arms it by dispatching the event rather than re-running loadScript (which would
    // stack up a second listener alongside the first — see working-calendar-overrides-details.test.js for the
    // same convention).
    beforeAll(() => {
        loadScript("wwwroot/assets/js/Tasks/RecurrenceRules/form.js");
    });

    beforeEach(() => {
        document.body.innerHTML = markup();
        window.fetch = mockFetch();
    });

    afterEach(() => {
        delete window.fetch;
        document.body.innerHTML = "";
    });

    const boot = async () => {
        document.dispatchEvent(new Event("DOMContentLoaded"));
        await new Promise((resolve) => setTimeout(resolve, 0));
    };

    it("fills the assignee picker from the { people, excluded } envelope — not just a bare array", async () => {
        await boot();

        const values = [...document.querySelector("[data-recurrence-assignee]").options].map((o) => o.value);
        expect(values).toContain(PERSON_ID);
    });

    it("keeps the positions and templates pickers working — those two endpoints DO answer bare arrays", async () => {
        await boot();

        const positionValues = [...document.querySelector("[data-recurrence-position]").options]
            .map((o) => o.value);
        const templateValues = [...document.querySelector("[data-recurrence-template]").options]
            .map((o) => o.value);

        expect(positionValues).toContain(POSITION_ID);
        expect(templateValues).toContain(TEMPLATE_ID);
    });

    it("still re-applies a saved assignee once the people envelope is unwrapped (edit mode)", async () => {
        // Mirrors what the Razor view does before select2/this script runs: the saved id is on the markup
        // before the option for it exists yet.
        document.querySelector("[data-recurrence-assignee]").innerHTML =
            `<option value="${PERSON_ID}" selected></option>`;

        await boot();

        expect(document.querySelector("[data-recurrence-assignee]").value).toBe(PERSON_ID);
    });
});
