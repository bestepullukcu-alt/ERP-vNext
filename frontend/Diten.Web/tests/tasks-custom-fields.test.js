const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 Phase 5 — the configurable fields the form had RESERVED SPACE FOR and never filled.
 *
 * Before this suite: `_Form.cshtml` shipped `#taskCustomFields` (born `d-none`) and an empty
 * `#taskCustomFieldsRow`, and NO script touched either id. A tenant could define a field on the Field
 * Definitions screen and it appeared nowhere on the create form — the container was a promise the code never
 * kept.
 *
 * The rules these tests hold:
 *   - a value type this round does not render is NOT half-rendered — the field is omitted entirely;
 *   - an option-driven field whose source cannot be resolved is NOT shown as an empty picker (BL-050: an
 *     unfillable selector is the same class of defect as a payload nobody reads);
 *   - a required field blocks the save on the client, and the server refuses it too (both sides hold);
 *   - the edit form comes back with the stored values in place.
 */
describe("MOD-0024 configurable task fields", () => {
  beforeEach(() => {
    delete global.TaskForm;
    document.body.innerHTML = "";
    loadScript("wwwroot/assets/js/Tasks/form.js");
  });

  const definition = (over) => ({
    code: "regulatory.phase",
    labelText: "Faz",
    valueType: "Text",
    section: "Regulatory",
    importance: "Secondary",
    isRequired: false,
    sortOrder: 10,
    optionsSourceKind: "None",
    optionsSourceKey: null,
    isActive: true,
    ...over
  });

  const container = () => {
    document.body.innerHTML = '<div id="taskCustomFieldsRow"></div>';
    return document.getElementById("taskCustomFieldsRow");
  };

  describe("which control a definition gets", () => {
    it("maps every value type this round supports", () => {
      const kind = (valueType) => global.TaskForm.customFieldControlKind(definition({ valueType }));

      expect(kind("Text")).toBe("text");
      expect(kind("Number")).toBe("number");
      expect(kind("Currency")).toBe("currency");
      expect(kind("Percentage")).toBe("percentage");
      expect(kind("Date")).toBe("date");
      expect(kind("DateTime")).toBe("datetime");
      expect(kind("Boolean")).toBe("boolean");
      expect(kind("Link")).toBe("link");
    });

    it("refuses to render a type it has no control for, rather than showing half of one", () => {
      // Reference points at an arbitrary entity and there is no generic resolver for what it points at; a bare
      // GUID box would be a control the user cannot fill correctly.
      expect(global.TaskForm.customFieldControlKind(definition({ valueType: "Reference" }))).toBeNull();
      expect(global.TaskForm.customFieldControlKind(definition({ valueType: "Nonsense" }))).toBeNull();
    });

    it("treats Status and Person as option-driven, never as free text", () => {
      expect(global.TaskForm.customFieldControlKind(
        definition({ valueType: "Status", optionsSourceKind: "BusinessReferenceData", optionsSourceKey: "phase" })
      )).toBe("select");
      expect(global.TaskForm.customFieldControlKind(definition({ valueType: "Person" }))).toBe("person");
    });

    it("gives a Status field with NO options source no control at all", () => {
      // It would be an empty dropdown: the exact BL-050 defect.
      expect(global.TaskForm.customFieldControlKind(definition({ valueType: "Status" }))).toBeNull();
    });

    it("refuses an options source declared on a numeric or date field", () => {
      // The server stores such a field as a decimal/date; an option code would never validate.
      expect(global.TaskForm.customFieldControlKind(
        definition({ valueType: "Number", optionsSourceKind: "PlatformLookup", optionsSourceKey: "currencies" })
      )).toBeNull();
    });
  });

  describe("rendering", () => {
    it("renders a text field into the row with its own label", () => {
      const row = container();
      const rendered = global.TaskForm.renderCustomFields(row, [definition()], {}, {});

      expect(rendered).toEqual(["regulatory.phase"]);
      const input = row.querySelector('[data-custom-field="regulatory.phase"]');
      expect(input).toBeTruthy();
      expect(input.tagName).toBe("INPUT");
      expect(row.textContent).toContain("Faz");
    });

    it("fills an option-driven field from its resolved source", () => {
      const row = container();
      const def = definition({
        code: "regulatory.market",
        valueType: "Status",
        labelText: "Pazar",
        optionsSourceKind: "BusinessReferenceData",
        optionsSourceKey: "country"
      });

      global.TaskForm.renderCustomFields(row, [def], {
        "regulatory.market": [{ value: "TR", label: "Türkiye" }, { value: "DE", label: "Almanya" }]
      }, { optionPlaceholder: "Seçiniz" });

      const select = row.querySelector('[data-custom-field="regulatory.market"]');
      expect(select.tagName).toBe("SELECT");
      expect([...select.options].map((o) => o.value)).toEqual(["", "TR", "DE"]);
      expect([...select.options].map((o) => o.textContent)).toEqual(["Seçiniz", "Türkiye", "Almanya"]);
    });

    it("does NOT render an option-driven field whose source could not be resolved, and says why", () => {
      const row = container();
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      const def = definition({
        code: "regulatory.market",
        valueType: "Status",
        optionsSourceKind: "BusinessReferenceData",
        optionsSourceKey: "does-not-exist"
      });

      const rendered = global.TaskForm.renderCustomFields(row, [def], {}, {});

      expect(rendered).toEqual([]);
      expect(row.querySelector('[data-custom-field="regulatory.market"]')).toBeNull();
      expect(warn).toHaveBeenCalled();
      expect(warn.mock.calls.flat().join(" ")).toContain("regulatory.market");
      warn.mockRestore();
    });

    it("does not render an unsupported type, and says which one it skipped", () => {
      const row = container();
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

      const rendered = global.TaskForm.renderCustomFields(
        row, [definition({ code: "x.ref", valueType: "Reference" })], {}, {});

      expect(rendered).toEqual([]);
      expect(warn.mock.calls.flat().join(" ")).toContain("x.ref");
      warn.mockRestore();
    });

    it("skips retired definitions entirely", () => {
      const row = container();
      const rendered = global.TaskForm.renderCustomFields(row, [definition({ isActive: false })], {}, {});
      expect(rendered).toEqual([]);
    });

    it("marks a required field so the user sees it before the save is refused", () => {
      const row = container();
      global.TaskForm.renderCustomFields(row, [definition({ isRequired: true })], {}, {});
      expect(row.querySelector(".text-danger")).toBeTruthy();
    });
  });

  describe("reading values back out", () => {
    it("sends the definition code and its declared value type", () => {
      const row = container();
      global.TaskForm.renderCustomFields(row, [definition()], {}, {});
      row.querySelector('[data-custom-field="regulatory.phase"]').value = "  Faz II  ";

      expect(global.TaskForm.readCustomFieldValues(row, [definition()])).toEqual([
        { definitionCode: "regulatory.phase", valueType: "Text", value: "Faz II" }
      ]);
    });

    it("omits an empty optional field rather than storing a blank value", () => {
      const row = container();
      global.TaskForm.renderCustomFields(row, [definition()], {}, {});
      expect(global.TaskForm.readCustomFieldValues(row, [definition()])).toEqual([]);
    });

    it("encodes a boolean as the invariant string the server parses", () => {
      const row = container();
      const def = definition({ code: "x.flag", valueType: "Boolean" });
      global.TaskForm.renderCustomFields(row, [def], {}, {});
      row.querySelector('[data-custom-field="x.flag"]').value = "true";

      expect(global.TaskForm.readCustomFieldValues(row, [def])).toEqual([
        { definitionCode: "x.flag", valueType: "Boolean", value: "true" }
      ]);
    });
  });

  describe("required fields block the save", () => {
    it("refuses a save when a required field is empty", () => {
      const check = global.TaskForm.validateCustomFields([definition({ isRequired: true })], []);
      expect(check.valid).toBe(false);
      expect(check.errors).toEqual(["regulatory.phase"]);
    });

    it("accepts the save once the required field carries a value", () => {
      const check = global.TaskForm.validateCustomFields(
        [definition({ isRequired: true })],
        [{ definitionCode: "regulatory.phase", valueType: "Text", value: "Faz II" }]);
      expect(check.valid).toBe(true);
    });

    it("does not demand a required field it never rendered", () => {
      // A required Reference field has no control this round; blocking on it would make the form unsavable.
      const check = global.TaskForm.validateCustomFields(
        [definition({ code: "x.ref", valueType: "Reference", isRequired: true })], []);
      expect(check.valid).toBe(true);
    });
  });

  describe("edit form hydration", () => {
    it("puts the stored values back into their controls", () => {
      const row = container();
      const defs = [definition(), definition({ code: "x.flag", valueType: "Boolean" })];
      global.TaskForm.renderCustomFields(row, defs, {}, {});

      global.TaskForm.writeCustomFieldValues(row, [
        { definitionCode: "regulatory.phase", valueType: "Text", value: "Faz III" },
        { definitionCode: "x.flag", valueType: "Boolean", value: "true" }
      ]);

      expect(row.querySelector('[data-custom-field="regulatory.phase"]').value).toBe("Faz III");
      expect(row.querySelector('[data-custom-field="x.flag"]').value).toBe("true");
      // And the round trip is lossless: what was hydrated is what would be sent back.
      expect(global.TaskForm.readCustomFieldValues(row, defs)).toEqual([
        { definitionCode: "regulatory.phase", valueType: "Text", value: "Faz III" },
        { definitionCode: "x.flag", valueType: "Boolean", value: "true" }
      ]);
    });

    it("hydrates a date value the browser control can actually display", () => {
      const row = container();
      const def = definition({ code: "x.when", valueType: "Date" });
      global.TaskForm.renderCustomFields(row, [def], {}, {});

      global.TaskForm.writeCustomFieldValues(row, [
        { definitionCode: "x.when", valueType: "Date", value: "2026-09-01T00:00:00+00:00" }
      ]);

      expect(row.querySelector('[data-custom-field="x.when"]').value).toBe("2026-09-01");
    });
  });

  describe("the payload carries the values", () => {
    it("passes fieldValues straight through to the create body", () => {
      const payload = global.TaskForm.buildCreatePayload({
        title: "t",
        dueAt: "2026-09-01",
        assignmentTarget: "SelfAssigned",
        fieldValues: [{ definitionCode: "regulatory.phase", valueType: "Text", value: "Faz II" }]
      });
      expect(payload.fieldValues).toEqual([
        { definitionCode: "regulatory.phase", valueType: "Text", value: "Faz II" }
      ]);
    });
  });

  describe("the wiring exists end to end", () => {
    const read = (relative) => fs.readFileSync(path.resolve(__dirname, "..", relative), "utf8");

    it("the form page actually calls the renderer — the container is no longer a promise", () => {
      const page = read("wwwroot/assets/js/Tasks/form-page.js");
      expect(page).toContain("taskCustomFieldsRow");
      expect(page).toContain("renderCustomFields");
      expect(page).toContain("readCustomFieldValues");
      expect(page).toContain("writeCustomFieldValues");
    });

    it("quick create hands over instead of repeating a refusal it can never satisfy", () => {
      /*
       * Quick create carries title/target/due/priority and nothing else. The moment a tenant marks a
       * configurable field required, no draft made there can pass — so it must hand the draft to the detailed
       * form rather than let the user press a button that cannot work.
       */
      const quick = read("wwwroot/assets/js/WorkCenterNext/quick-create.js");
      expect(quick).toContain("TASK_FIELD_VALUE_INVALID");
      expect(quick).toMatch(/TASK_FIELD_VALUE_INVALID[\s\S]{0,400}?openDetailed\(\)/);
    });

    it("the api client can read definitions AND resolve one field's options", () => {
      const api = read("wwwroot/assets/js/Tasks/api.js");
      expect(api).toContain("field-definitions");
      expect(api).toContain("/options");
    });

    it("the web tier proxies the options route — a route Platform exposes and the proxy does not is a 404", () => {
      const proxy = read("Controllers/TasksController.cs");
      expect(proxy).toContain('api/field-definitions/{code}/options');
      expect(proxy).toContain("/api/v1/tasks/field-definitions/");
    });

    it("Platform exposes the options route under an ordinary task READ, not the manage permission", () => {
      const controller = fs.readFileSync(path.resolve(
        __dirname, "..", "..", "..", "services", "Diten.Platform", "src", "Diten.Platform.API",
        "Controllers", "TasksController.cs"), "utf8");
      const action = controller.slice(controller.indexOf('field-definitions/{code}/options'));
      expect(action.slice(0, 400)).toContain("TaskPermissions.Read");
    });
  });

  describe("7-language parity for the new strings", () => {
    const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
    const NEW_KEYS = ["CustomFieldOptionPlaceholder", "CustomFieldBooleanYes", "CustomFieldBooleanNo"];

    const entries = (locale) => {
      const xml = fs.readFileSync(path.resolve(
        __dirname, "..", "Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`), "utf8");
      const out = {};
      const pattern = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
      let match;
      while ((match = pattern.exec(xml)) !== null) { out[match[1]] = match[2]; }
      return out;
    };

    it("ships every new string in all seven languages, none of them the English value", () => {
      const byLocale = {};
      LOCALES.forEach((locale) => { byLocale[locale] = entries(locale); });

      NEW_KEYS.forEach((key) => {
        LOCALES.forEach((locale) => { expect(byLocale[locale][key], `${key}/${locale}`).toBeTruthy(); });
        expect(byLocale.tr[key]).not.toBe(byLocale.en[key]);
        expect(byLocale.ru[key]).not.toBe(byLocale.en[key]);
      });
    });

    it("serializes them into the payload the JS reads", () => {
      const bridge = fs.readFileSync(path.resolve(
        __dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
      NEW_KEYS.forEach((key) => { expect(bridge).toContain(key); });
    });
  });
});
