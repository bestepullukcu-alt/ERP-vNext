const { loadScript } = require("./load-script");

/*
 * ══ BOTH PEOPLE LISTS ANSWER THE ARRAY (BL-491, CT acceptance) ══════════════════════════════════════
 *
 * The wire answers `{ people, excluded }` for BOTH lookups. `assignablePeople` has opened that envelope inside
 * TasksApi since BL-113, because three callers in three rounds opened it wrongly by hand — and a caller that gets it
 * wrong does not crash, it concludes "nobody" and refuses politely for ever.
 *
 * `decisionMakers` kept the raw object while it had ONE caller. The Task Center's delegate window is its second, and
 * a second hand-written unwrap is how that defect comes back. So the shape stops in TasksApi for this list too.
 *
 * Real Tasks/api.js over a stubbed `fetch` — the unwrap measured is the product's, not a copy of it.
 */
const AYSE = { userId: "cccccccc-cccc-cccc-cccc-cccccccccccc", displayName: "Ayşe Kaya" };

describe("TasksApi: the two people lists have one shape", () => {
  let asked;

  const answer = (data, status = 200) => {
    global.fetch = async (url) => {
      asked.push(url);
      return { ok: status >= 200 && status < 300, status, json: async () => ({ data }) };
    };
  };

  beforeEach(() => {
    asked = [];
    loadScript("wwwroot/assets/js/Tasks/api.js");
  });

  afterEach(() => { delete global.fetch; });

  it.each([
    ["decisionMakers", "/Tasks/api/decision-makers"],
    ["assignablePeople", "/Tasks/api/assignable-people"]
  ])("%s hands its caller the array, from its own endpoint", async (call, url) => {
    answer({ people: [AYSE], excluded: { total: 0 } });

    const result = await window.TasksApi[call]();

    expect(asked).toEqual([url]);
    expect(result.ok).toBe(true);
    expect(result.data, `${call} still hands out the { people, excluded } envelope`).toEqual([AYSE]);
  });

  it.each([["decisionMakers"], ["assignablePeople"]])(
    "%s answers an empty array — never the object — when the envelope carries no list", async (call) => {
      answer({ excluded: { total: 3 } });

      expect((await window.TasksApi[call]()).data).toEqual([]);
    });

  it.each([["decisionMakers"], ["assignablePeople"]])(
    "%s keeps a failure a failure: not ok, its status, and an empty list", async (call) => {
      answer(null, 403);

      const result = await window.TasksApi[call]();

      expect(result.ok).toBe(false);
      expect(result.status).toBe(403);
      expect(result.data).toEqual([]);
    });
});
