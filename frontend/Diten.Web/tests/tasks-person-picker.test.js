const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 §K6.4 — assigning to a person must be usable.
 *
 * Before this, "Bir kişiye ata" was a bare text input: the user had to know and type a user GUID. The picker
 * replaces it, and the rules it must not break are:
 *   - a raw user id is NEVER shown to a human;
 *   - every option carries position AND unit, because two people can hold the same position in different
 *     facilities (the K4 wrong-facility trap, transposed onto people);
 *   - an empty list is EXPLAINED (nobody holds a position) rather than rendered as a blank dropdown.
 */
describe("MOD-0024 person picker", () => {
  const ALICE = "dddddddd-dddd-dddd-dddd-dddddddddddd";
  const BOB = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

  const labels = {
    placeholder: "Kişi seçin…",
    empty: "Henüz hiç kimsenin pozisyonu yok…",
    nameUnavailable: "Ad bilgisi yok"
  };

  const person = (overrides = {}) => ({
    userId: ALICE,
    displayName: "Selin Aras",
    positionId: "11111111-1111-1111-1111-111111111111",
    positionCode: "QA-1",
    positionName: "QA Specialist",
    organizationUnitId: "22222222-2222-2222-2222-222222222222",
    organizationUnitCode: "FAC-A",
    organizationUnitName: "Facility A",
    ...overrides
  });

  beforeEach(() => {
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
  });

  describe("labels", () => {
    it("reads 'Name — Position — Unit'", () => {
      expect(global.TaskForm.formatPersonLabel(person(), labels.nameUnavailable))
        .toBe("Selin Aras — QA Specialist — Facility A");
    });

    it("distinguishes two people holding the SAME position in different facilities", () => {
      const a = person({ displayName: "Selin Aras" });
      const b = person({ userId: BOB, displayName: "Deniz Koç", organizationUnitName: "Facility B" });

      const labelA = global.TaskForm.formatPersonLabel(a, labels.nameUnavailable);
      const labelB = global.TaskForm.formatPersonLabel(b, labels.nameUnavailable);

      expect(labelA).not.toBe(labelB);
      expect(labelA).toContain("Facility A");
      expect(labelB).toContain("Facility B");
    });

    it("says the name is unavailable instead of falling back to the id", () => {
      const label = global.TaskForm.formatPersonLabel(
        person({ displayName: null }), labels.nameUnavailable);

      expect(label).toBe("Ad bilgisi yok — QA Specialist — Facility A");
      expect(label).not.toContain(ALICE);
    });

    it("falls back to codes when names are absent", () => {
      const label = global.TaskForm.formatPersonLabel(
        person({ displayName: "X", positionName: null, organizationUnitName: null }), labels.nameUnavailable);

      expect(label).toBe("X — QA-1 — FAC-A");
    });
  });

  describe("options", () => {
    beforeEach(() => { document.body.innerHTML = '<select id="who"></select>'; });

    const select = () => document.getElementById("who");

    it("renders one option per person, plus a placeholder", () => {
      global.TaskForm.renderPersonOptions(
        select(), [person(), person({ userId: BOB, displayName: "Deniz Koç" })], labels);

      const options = [...select().options];
      expect(options).toHaveLength(3);
      expect(options[0].textContent).toBe("Kişi seçin…");
      expect(options[1].value).toBe(ALICE);
      expect(options[2].value).toBe(BOB);
      expect(select().disabled).toBe(false);
    });

    it("never renders a GUID as the visible text", () => {
      global.TaskForm.renderPersonOptions(select(), [person(), person({ userId: BOB, displayName: null })], labels);

      [...select().options].forEach((option) => {
        expect(option.textContent).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}/i);
      });
      // The id still travels as the VALUE — that is what gets submitted.
      expect([...select().options].map((o) => o.value)).toContain(ALICE);
    });

    it("explains an empty list and disables the control", () => {
      global.TaskForm.renderPersonOptions(select(), [], labels);

      expect(select().options).toHaveLength(1);
      expect(select().options[0].textContent).toBe(labels.empty);
      expect(select().disabled).toBe(true);
      // Nothing selectable: there is genuinely nobody to assign to.
      expect([...select().options].filter((o) => !o.disabled)).toHaveLength(0);
    });

    it("treats a failed lookup the same as an empty one — explained, not blank", () => {
      global.TaskForm.renderPersonOptions(select(), null, labels);

      expect(select().options[0].textContent).toBe(labels.empty);
      expect(select().disabled).toBe(true);
    });
  });

  describe("wiring (source contract)", () => {
    const read = (...parts) => fs.readFileSync(path.resolve(__dirname, "..", ...parts), "utf8");

    it("both forms use a select, not the old text input", () => {
      const quick = read("Views", "Tasks", "_QuickCreateOffcanvas.cshtml");
      const detailed = read("Views", "Tasks", "_Form.cshtml");

      // Matched on the ELEMENT, not on an exact class string: the quick surface later gained `select2` on the
      // same control (it now uses the full form's enhancer), and pinning the literal made this test fail for a
      // change that is exactly what it wanted — a real picker rather than a text box.
      expect(quick).toMatch(/<select class="[^"]*form-select[^"]*" id="quickAssignee"/);
      expect(detailed).toMatch(/<select class="[^"]*form-select[^"]*" id="taskAssignee"/);
      // The bare input demanded a GUID.
      expect(quick).not.toMatch(/<input[^>]*id="quickAssignee"/);
      expect(detailed).not.toMatch(/<input[^>]*id="taskAssignee"/);
    });

    it("both surfaces load the people lookup", () => {
      expect(read("wwwroot", "assets", "js", "WorkCenterNext", "quick-create.js"))
        .toContain("assignablePeople()");
      expect(read("wwwroot", "assets", "js", "Tasks", "form-page.js"))
        .toContain("assignablePeople()");
    });

    it("asks for its labels in camelCase, the case the bridge emits", () => {
      const sources = [
        read("wwwroot", "assets", "js", "WorkCenterNext", "quick-create.js"),
        read("wwwroot", "assets", "js", "Tasks", "form-page.js")
      ].join("\n");

      ["assigneeSelectPlaceholder", "assigneeEmpty", "personNameUnavailable"].forEach((key) => {
        expect(sources).toContain(`t('${key}')`);
      });
    });
  });
});
