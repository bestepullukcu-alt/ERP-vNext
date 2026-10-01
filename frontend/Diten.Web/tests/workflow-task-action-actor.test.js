const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WORKFLOW-APPROVAL-STATUS-01 (B4) — the actor of a MOD-0023 task action is ALWAYS the signed-in user, taken by the
 * server from the session. The Platform › Workflow screen used to ask for an "Actor Id" in a free-text box and post it,
 * which let anyone with the approve permission act as somebody else. Measured on the PRODUCTION files: the box is gone
 * from both views, neither page script reads it, and the API client drops any actor field a caller still passes.
 */

const WEB = path.resolve(__dirname, "..");
const VIEWS = ["Views/Platform/Workflow/Tasks.cshtml", "Views/Platform/Workflow/Index.cshtml"];
const SCRIPTS = ["wwwroot/assets/js/Platform/Workflow/index.js", "wwwroot/assets/js/Platform/Workflow/tasks.js"];
const ACTIONS = ["approveTask", "rejectTask", "delegateTask", "requestInfoTask", "cancelTask"];

describe("B4: the Workflow screen never names the actor", () => {
  it.each(VIEWS)("%s has no Actor Id field", (view) => {
    expect(fs.readFileSync(path.join(WEB, view), "utf8")).not.toContain("wf-taskaction-actorid");
  });

  it.each(SCRIPTS)("%s neither reads nor requires an actor", (script) => {
    const source = fs.readFileSync(path.join(WEB, script), "utf8");
    expect(source).not.toContain("wf-taskaction-actorid");
    expect(source).not.toMatch(/payload\.actorId/);
  });

  describe("the API client", () => {
    let bodies;

    beforeEach(() => {
      bodies = [];
      delete window.WorkflowApi;
      global.fetch = vi.fn(async (_url, init) => {
        bodies.push(init && init.body ? JSON.parse(init.body) : null);
        return { ok: true, status: 200, statusText: "OK", headers: { get: () => null }, text: async () => "{}" };
      });
      loadScript("wwwroot/assets/js/Platform/Workflow/workflow.api.js");
    });

    it.each(ACTIONS)("%s sends no actorId even when the caller passes one", async (action) => {
      await window.WorkflowApi[action]("5f1e0000-0000-0000-0000-000000000001", {
        actorId: "someone-else", ActorId: "someone-else", reasonCode: "OK", idempotencyKey: "k-1", comment: "c"
      });

      expect(bodies).toHaveLength(1);
      expect(bodies[0]).not.toHaveProperty("actorId");
      expect(bodies[0]).not.toHaveProperty("ActorId");
      // Non-vacuity: the rest of the payload still travels.
      expect(bodies[0]).toMatchObject({ reasonCode: "OK", idempotencyKey: "k-1", comment: "c" });
    });
  });
});
