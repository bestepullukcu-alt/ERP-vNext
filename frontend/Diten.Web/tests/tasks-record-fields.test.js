const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 — a configurable field whose values come from ANOTHER MODULE'S RECORDS.
 *
 * This is not a new idea and it is not modelled as one. SAP calls it a check table with an F4 search help,
 * Oracle calls it a table-validated value set behind a DFF, ServiceNow calls it a reference field. All three say
 * the same sentence: the ADMINISTRATOR defines the field, and the VALUES come from somewhere else that already
 * owns them.
 *
 * The rules this suite holds, each one a defect this repo has already paid for once:
 *   - a record-backed field gets a SEARCHABLE picker, never a dropdown: five thousand products do not fit in an
 *     <option> list, and a picker that silently truncates is worse than one that searches;
 *   - what is STORED is the identity and what is SHOWN is the business key and the name — never the raw GUID
 *     (BL-049), and never the name alone (a renamed record must change on the task too);
 *   - an unresolvable source hides the field and REPORTS why, exactly as an unresolvable option list already
 *     does — one rule, not two;
 *   - BOTH first sources resolve down the SAME path. A source with a special case is not a contract.
 */

const repoRoot = path.resolve(__dirname, "../../..");
const FORM_JS = "wwwroot/assets/js/Tasks/form.js";

describe("MOD-0024 module-record fields", () => {
  beforeEach(() => {
    delete global.TaskForm;
    document.body.innerHTML = "";
    loadScript(FORM_JS);
  });

  // A definition pointing at another module's records: Reference is the value type whose value IS an identity,
  // and ModuleRecord is the source kind that says which module owns it.
  const recordDefinition = (over) => ({
    code: "delivery.department",
    labelText: "Departman",
    valueType: "Reference",
    section: "Delivery",
    importance: "Secondary",
    isRequired: false,
    sortOrder: 10,
    optionsSourceKind: "ModuleRecord",
    optionsSourceKey: "organization-unit",
    isActive: true,
    ...over
  });

  // What the server sends back for one record, flattened to the option shape every source shares.
  const record = (value, label, secondary) => ({ value, label, secondary });

  const UNIT_A = "3f1b2a2c-0000-4000-8000-000000000001";
  const UNIT_B = "3f1b2a2c-0000-4000-8000-000000000002";
  const POSITION_A = "7c9d4e5f-0000-4000-8000-000000000001";

  describe("which control a record-backed definition gets", () => {
    it("gives a ModuleRecord-backed Reference field a searchable record picker", () => {
      expect(global.TaskForm.customFieldControlKind(recordDefinition())).toBe("record");
    });

    it("still gives a Reference field with NO source no control at all", () => {
      // Unchanged on purpose: without a source the only control we could offer is a raw GUID box, which is a
      // control the user cannot fill correctly.
      expect(global.TaskForm.customFieldControlKind(
        recordDefinition({ optionsSourceKind: "None", optionsSourceKey: null }))).toBeNull();
    });

    it("refuses a record source declared on a value type that cannot hold an identity", () => {
      // A ModuleRecord source stores an identity. On a Number or a Date the server refuses the value, so the
      // form must not offer a control that can only produce a refusal.
      ["Number", "Date", "Boolean", "Currency"].forEach((valueType) => {
        expect(global.TaskForm.customFieldControlKind(recordDefinition({ valueType })), valueType).toBeNull();
      });
    });
  });

  describe("rendering the picker", () => {
    const renderOne = (definition, options) => {
      const row = document.createElement("div");
      document.body.appendChild(row);
      const rendered = global.TaskForm.renderCustomFields(row, [definition], options, {
        optionPlaceholder: "Seçiniz",
        recordSearchPlaceholder: "Aramak için yazın",
        recordNoMatches: "Eşleşen kayıt yok"
      });
      return { row, rendered };
    };

    it("marks the control as server-paged, which is what makes its search reach the server", () => {
      /*
       * This used to assert a second control — a hand-rolled search <input> beside the picker — because a
       * dropdown cannot hold five thousand records and select2's local search filters only what is loaded. The
       * need is unchanged; the shape is not. select2's `ajax` gives the picker its own server-backed search, so
       * the field is ONE control again and this attribute is the flag enhanceSelects keys that decision off.
       * The search itself is driven end to end in tasks-form-pickers-dates-governance.
       */
      const { row, rendered } = renderOne(recordDefinition(), {
        "delivery.department": [record(UNIT_A, "Kalite Güvence", "QA-01")]
      });

      expect(rendered).toEqual(["delivery.department"]);
      expect(row.querySelector('[data-custom-field-search="delivery.department"]'),
        "the second search box is back — one field, one control").toBeNull();
      const control = row.querySelector('[data-custom-field="delivery.department"]');
      expect(control.getAttribute("data-custom-field-record")).toBe("1");
    });

    it("puts the identity in the option VALUE and the business key beside the name in its text", () => {
      const { row } = renderOne(recordDefinition(), {
        "delivery.department": [record(UNIT_A, "Kalite Güvence", "QA-01")]
      });

      const control = row.querySelector('[data-custom-field="delivery.department"]');
      const option = [...control.options].find((o) => o.value === UNIT_A);
      expect(option, "the record's identity is not the option value — the task would store a label").toBeTruthy();
      // BL-049: the reader recognises the record by its key and its name. The identity is carried, not shown.
      expect(option.textContent).toContain("Kalite Güvence");
      expect(option.textContent).toContain("QA-01");
      expect(option.textContent).not.toContain(UNIT_A);
    });

    it("does NOT render a record field whose source could not be resolved, and says why", () => {
      const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
      const { row, rendered } = renderOne(recordDefinition(), {});

      expect(rendered).toEqual([]);
      expect(row.querySelector('[data-custom-field="delivery.department"]')).toBeNull();
      expect(warn).toHaveBeenCalled();
      warn.mockRestore();
    });
  });

  describe("both first sources travel the same path", () => {
    /*
     * The only honest test of a general contract: two sources, no special case. Organization units and positions
     * are rendered by the SAME code, read by the SAME code, and hydrated by the SAME code. If either needs its
     * own branch, the contract is not general and the third source (Product) will rewrite it.
     */
    const departmentField = recordDefinition();
    const positionField = recordDefinition({
      code: "delivery.position",
      labelText: "Pozisyon",
      optionsSourceKey: "position"
    });

    it("gives both sources the same control kind", () => {
      const department = global.TaskForm.customFieldControlKind(departmentField);
      // Non-vacuity: "both are null" would satisfy an equality assertion forever, and null is exactly what this
      // function returned before the round. The kind has to be a real control AND the same one.
      expect(department).not.toBeNull();
      expect(global.TaskForm.customFieldControlKind(positionField)).toBe(department);
    });

    it("renders, reads and hydrates both without naming either source", () => {
      const row = document.createElement("div");
      document.body.appendChild(row);

      global.TaskForm.renderCustomFields(row, [departmentField, positionField], {
        "delivery.department": [record(UNIT_A, "Kalite Güvence", "QA-01")],
        "delivery.position": [record(POSITION_A, "Kalite Uzmanı", "QA-SPEC · Tesis A")]
      }, {});

      global.TaskForm.writeCustomFieldValues(row, [
        { definitionCode: "delivery.department", valueType: "Reference", value: UNIT_A },
        { definitionCode: "delivery.position", valueType: "Reference", value: POSITION_A }
      ]);

      const read = global.TaskForm.readCustomFieldValues(row, [departmentField, positionField]);
      expect(read).toEqual([
        { definitionCode: "delivery.department", valueType: "Reference", value: UNIT_A },
        { definitionCode: "delivery.position", valueType: "Reference", value: POSITION_A }
      ]);

      // Non-vacuity: the source keys differ, so a shared path really was exercised twice.
      expect(departmentField.optionsSourceKey).not.toBe(positionField.optionsSourceKey);
    });

    it("has no source key written into the form's own code", () => {
      /*
       * The contract lives in the DEFINITION, not in the client. A client that knows the word
       * "organization-unit" is a client that must be edited when the Product module arrives — which is the one
       * thing this round exists to prevent.
       */
      const source = fs.readFileSync(path.join(repoRoot, "frontend/Diten.Web", FORM_JS), "utf8")
        .replace(/\/\*[\s\S]*?\*\//g, "")
        .replace(/\/\/[^\n]*/g, "");
      ["organization-unit", "position", "product"].forEach((key) => {
        expect(source, `form.js names the source "${key}" in code`).not.toContain(`"${key}"`);
        expect(source, `form.js names the source "${key}" in code`).not.toContain(`'${key}'`);
      });
    });
  });

  describe("hydration on edit", () => {
    const buildRow = () => {
      const row = document.createElement("div");
      document.body.appendChild(row);
      global.TaskForm.renderCustomFields(row, [recordDefinition()], {
        "delivery.department": [record(UNIT_A, "Kalite Güvence", "QA-01")]
      }, {});
      return row;
    };

    it("selects a stored identity whose record is already in the first page", () => {
      const row = buildRow();
      global.TaskForm.writeCustomFieldValues(row, [
        { definitionCode: "delivery.department", valueType: "Reference", value: UNIT_A }
      ]);
      expect(row.querySelector('[data-custom-field="delivery.department"]').value).toBe(UNIT_A);
    });

    it("selects a stored identity that the first page never contained", () => {
      /*
       * THE DATA-LOSS TRAP, and the reason this test exists at all. The search endpoint answers with the first
       * page of records; a task saved months ago points at a record that page does not contain. A <select> given
       * a value with no matching <option> silently keeps its OLD value — so the edit would post back a different
       * department, or none. Yesterday's round caught exactly this shape on date fields.
       */
      const row = buildRow();
      global.TaskForm.writeCustomFieldValues(
        row,
        [{ definitionCode: "delivery.department", valueType: "Reference", value: UNIT_B }],
        { "delivery.department": [record(UNIT_B, "Ruhsatlandırma", "REG-02")] });

      const control = row.querySelector('[data-custom-field="delivery.department"]');
      expect(control.value, "a stored record outside the first page was silently dropped").toBe(UNIT_B);
      expect([...control.options].find((o) => o.value === UNIT_B).textContent).toContain("Ruhsatlandırma");
    });

    it("never falls back to printing the identity when the record cannot be resolved", () => {
      // BL-049. A record deleted upstream still has an id on the task; the user must not be shown a GUID.
      const row = buildRow();
      global.TaskForm.writeCustomFieldValues(
        row,
        [{ definitionCode: "delivery.department", valueType: "Reference", value: UNIT_B }],
        {},
        { recordUnavailable: "Kayıt bulunamadı" });

      const control = row.querySelector('[data-custom-field="delivery.department"]');
      expect(control.value).toBe(UNIT_B);
      const selected = [...control.options].find((o) => o.value === UNIT_B);
      expect(selected.textContent).toBe("Kayıt bulunamadı");
      expect(selected.textContent).not.toContain(UNIT_B);
    });
  });

  describe("the wiring exists end to end", () => {
    const read = (relative) => fs.readFileSync(path.join(repoRoot, relative), "utf8");

    it("the api client can search a field's records and resolve stored ones", () => {
      const api = read("frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js");
      expect(api, "TasksApi cannot search records").toMatch(/fieldRecords\s*:/);
    });

    it("the web tier proxies the records route — a route Platform exposes and the proxy does not is a 404", () => {
      const proxy = read("frontend/Diten.Web/Controllers/TasksController.cs");
      expect(proxy).toContain("api/field-definitions/{code}/records");
    });

    it("Platform exposes the records route under an ordinary task READ, not the manage permission", () => {
      // Same reasoning the options route already carries: gating the picker behind the ADMIN permission leaves
      // every ordinary user with a picker they cannot fill.
      const controller = read("services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs");
      const route = controller.indexOf('[HttpGet("field-definitions/{code}/records")]');
      expect(route, "Platform does not expose the records route").toBeGreaterThan(-1);
      expect(controller.slice(route, route + 200)).toContain("TaskPermissions.Read");
    });

    it("the form page resolves record sources through the SAME loader as every other option source", () => {
      const page = read("frontend/Diten.Web/wwwroot/assets/js/Tasks/form-page.js");
      expect(page, "record fields are loaded outside loadCustomFieldOptions").toMatch(
        /loadCustomFieldOptions[\s\S]*?fieldRecords/);
    });

    it("the admin screen offers the sources instead of asking for them to be typed", () => {
      // İŞ 1. A key that can only be chosen cannot be mistyped into a field that silently never appears.
      const form = read("frontend/Diten.Web/Views/Tasks/FieldDefinitions/_Form.cshtml");
      expect(form, "the source key is still a free-text box").not.toMatch(/<input[^>]*asp-for="OptionsSourceKey"/);
      expect(form).toMatch(/<select[^>]*asp-for="OptionsSourceKey"/);
      // And the new kind is offered at all.
      expect(form).toContain('<option value="ModuleRecord">');
    });

    it.each([
      ["frontend/Diten.Web/Views/Tasks/Create.cshtml"],
      ["frontend/Diten.Web/Views/Tasks/Edit.cshtml"]
    ])("%s loads the modal helper its own script calls", (view) => {
      /*
       * FOUND BY RUNNING THE PAGE, not by a test — which is the entire reason this one exists.
       *
       * `form-page.js` reports every outcome through `DitenModal`, and neither page ever loaded
       * `shared/premium-modal.js`. So a successful create THREW on `DitenModal.success` after the 201: no
       * acknowledgement, no navigation, the form just sat there. The user's only reasonable move is to click
       * Save again — and the live run did exactly that, producing TWO tasks from one intent. A refusal was
       * worse: `DitenModal.error` threw too, so the server's reason code reached nobody at all.
       *
       * Asserted as "the page loads what its script calls" rather than as "premium-modal.js is present", so a
       * third page copying the same script list fails here instead of in front of a user.
       */
      const page = read(view);
      const scripts = [...page.matchAll(/<script src="~\/assets\/js\/([^"]+)"/g)].map((m) => m[1]);
      const loaded = new Set(scripts);

      // Non-vacuity: the scan has to have found this page's real script list.
      expect(scripts.length, `${view}: no asset scripts found to check`).toBeGreaterThan(0);
      expect(loaded.has("Tasks/form-page.js"), `${view} does not load form-page.js`).toBe(true);

      const helper = read("frontend/Diten.Web/wwwroot/assets/js/Tasks/form-page.js");
      if (/\bDitenModal\./.test(helper)) {
        expect(loaded.has("shared/premium-modal.js"),
          `${view} calls DitenModal but never loads shared/premium-modal.js`).toBe(true);
      }
    });

    it("Platform exposes the source list under the MANAGE permission, not the read one", () => {
      // Shaping a definition is the administrative act; filling one is not. The two routes are guarded apart.
      const controller = read("services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs");
      const route = controller.indexOf('[HttpGet("field-definitions/option-sources")]');
      expect(route, "Platform does not expose the option-sources route").toBeGreaterThan(-1);
      expect(controller.slice(route, route + 200)).toContain("TaskPermissions.FieldDefinitionsManage");
    });
  });

  describe("7-language parity for the new strings", () => {
    const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];

    // Every string this round added, by the resx it lives in.
    const NEW_STRINGS = {
      "Views/Tasks/TasksIndex": [
        "CustomFieldRecordSearchPlaceholder",
        "CustomFieldRecordUnavailable",
        "ErrorFieldOptionSourceInvalid"
      ],
      "Views/Tasks/FieldDefinitions/TaskFieldDefinitionsIndex": [
        "OptionsSourceModuleRecord",
        "OptionsSourceKeyHint",
        "OptionsSourceKeySelect",
        "OptionsSourceKeyNone",
        "OptionsSourceKeyLoadFailed",
        // The source names themselves. Platform ships no tenant resx, so the SOURCE carries a stable key and
        // these seven files carry the words — the bridge the tenant navigation already uses.
        "OptionSource.organization-unit",
        "OptionSource.position",
        "OptionSource.countries",
        "OptionSource.currencies",
        "OptionSource.languages",
        "OptionSource.timezones"
      ]
    };

    const entries = (resx, locale) => {
      const xml = fs.readFileSync(
        path.join(repoRoot, "frontend/Diten.Web/Resources", `${resx}.${locale}.resx`), "utf8");
      const out = {};
      const pattern = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
      let match;
      while ((match = pattern.exec(xml)) !== null) { out[match[1]] = match[2]; }
      return out;
    };

    it("ships every new string in all seven languages, none of them left at the English value", () => {
      Object.entries(NEW_STRINGS).forEach(([resx, keys]) => {
        const byLocale = {};
        LOCALES.forEach((locale) => { byLocale[locale] = entries(resx, locale); });

        keys.forEach((key) => {
          LOCALES.forEach((locale) => {
            expect(byLocale[locale][key], `${resx}: ${key} missing in ${locale}`).toBeTruthy();
          });
          // A copied English value is the shape a "7 languages" claim usually fails in. Checked against the
          // three scripts that could not plausibly share a word with English.
          ["tr", "ru", "zh"].forEach((locale) => {
            expect(byLocale[locale][key], `${resx}: ${key} is still English in ${locale}`)
              .not.toBe(byLocale.en[key]);
          });
        });
      });
    });

    it("serializes the task-form strings into the payload the JS reads", () => {
      // A translated string nobody serialises reaches the screen as its own key — the defect that put "Edit" on
      // the recurrence screen in all seven languages.
      const bridge = fs.readFileSync(
        path.join(repoRoot, "frontend/Diten.Web/Views/Tasks/_IndexL10n.cshtml"), "utf8");
      NEW_STRINGS["Views/Tasks/TasksIndex"].forEach((key) => { expect(bridge).toContain(key); });
    });

    it("hands the source names to the admin screen BY PREFIX, so a new source needs no code change", () => {
      /*
       * The requirement in one assertion: adding the Product module's source must be adding a RECORD, not
       * editing code. If this partial listed the sources it knows, the third one would need a line here.
       */
      const form = fs.readFileSync(
        path.join(repoRoot, "frontend/Diten.Web/Views/Tasks/FieldDefinitions/_Form.cshtml"), "utf8");
      expect(form).toContain("GetAllStrings");
      expect(form).toContain("OptionSource.");
      ["organization-unit", "position"].forEach((key) => {
        expect(form, `the form names the source "${key}"`).not.toContain(`OptionSource.${key}`);
      });
    });
  });
});
