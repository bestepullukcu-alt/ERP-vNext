const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-351 — TASK_ASSIGNEE_NOT_ASSIGNABLE used to be declared TWICE in REASON_CODE_MESSAGE_KEYS: once for the
 * inquire "waiting on" refusal, mapping to 'errorWaitingOnNotAssignable', and once for the assign/reassign
 * guard, mapping to 'errorAssigneeNotAssignable'. A JS object literal keeps only the LAST value written for a
 * repeated key, so the first was dead on arrival and every reader of the code — including the waiting-on
 * refusal itself — got the assignment sentence instead of its own. The backend returns the SAME code from both
 * InquireTaskItemHandler and TaskAssignmentGuard and must not change; the split has to live here.
 *
 * The fix keeps exactly one base mapping (assign/reassign — the majority of callers) and adds
 * INQUIRE_REASON_CODE_OVERRIDES plus a second failureMessage() argument for the one caller that needs the other
 * sentence.
 */
const API_JS = path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Tasks", "api.js");

describe("BL-351: REASON_CODE_MESSAGE_KEYS has no duplicate keys", () => {
    it("declares every reason code exactly once in the base map", () => {
        const source = fs.readFileSync(API_JS, "utf8");
        const start = source.indexOf("const REASON_CODE_MESSAGE_KEYS = {");
        expect(start, "REASON_CODE_MESSAGE_KEYS is not declared in Tasks/api.js").toBeGreaterThan(-1);
        const end = source.indexOf("\n    };", start);
        expect(end, "could not find the closing brace of REASON_CODE_MESSAGE_KEYS").toBeGreaterThan(start);
        const body = source.slice(start, end);

        // Anchored to the START of the line so a colon inside a prose comment (which always begins with `//`,
        // `*` or `/*`, never with an identifier character) can never be mistaken for a `KEY: 'value'` entry.
        const keys = [...body.matchAll(/^\s*([A-Za-z][A-Za-z0-9_]*)\s*:\s*'[^']*'/gm)].map(([, key]) => key);
        expect(keys.length, "the scan found no keys at all — the marker/regex pair is broken").toBeGreaterThan(30);

        const counts = new Map();
        keys.forEach((key) => counts.set(key, (counts.get(key) || 0) + 1));
        const duplicates = [...counts.entries()].filter(([, count]) => count > 1).map(([key]) => key);

        // Non-vacuity: this is the exact failure BL-351 measured. If this assertion is ever satisfied trivially
        // (no keys found) the length check above already caught it.
        expect(duplicates).toEqual([]);
    });

    it("still maps TASK_ASSIGNEE_NOT_ASSIGNABLE to the assignment sentence — the fix removes the duplicate, not the code", () => {
        const source = fs.readFileSync(API_JS, "utf8");
        expect(source).toMatch(/TASK_ASSIGNEE_NOT_ASSIGNABLE:\s*'errorAssigneeNotAssignable'/);
    });
});

describe("BL-351: failureMessage() resolves the two refusals to two different sentences", () => {
    const loadApi = () => {
        delete global.TasksApi;
        // Echoes the key back, so an assertion naming a message key asserts the key the code chose rather than
        // a translation that could drift independently (same convention as the other api.js suites).
        global.TasksL10n = { t: (key) => key };
        loadScript("wwwroot/assets/js/Tasks/api.js");
        return global.TasksApi;
    };

    it("keeps the assignment sentence as the default for every ordinary caller (assign/reassign)", () => {
        const api = loadApi();

        const message = api.failureMessage({ ok: false, status: 400, reasonCode: "TASK_ASSIGNEE_NOT_ASSIGNABLE" });

        expect(message).toBe("errorAssigneeNotAssignable");
    });

    it("resolves to the waiting-on sentence ONLY when the inquire override is passed", () => {
        const api = loadApi();

        const message = api.failureMessage(
            { ok: false, status: 400, reasonCode: "TASK_ASSIGNEE_NOT_ASSIGNABLE" },
            api.INQUIRE_REASON_CODE_OVERRIDES);

        expect(message).toBe("errorWaitingOnNotAssignable");
    });

    it("leaves every other reason code alone when the inquire override is passed but does not name it", () => {
        const api = loadApi();

        const message = api.failureMessage(
            { ok: false, status: 409, reasonCode: "TASK_CONCURRENCY_CONFLICT" },
            api.INQUIRE_REASON_CODE_OVERRIDES);

        expect(message).toBe("errorConcurrencyRefreshed");
    });
});
