const { loadScript } = require("./load-script");

// MOD-0024 Phase 1 — the task form's decision logic. These are the rules that keep pooled work out of the wrong
// facility (K4), keep the three assignment targets coherent (K5), and make the quick↔detailed handover lossless
// (K9/DEV-1).
describe("MOD-0024 task form", () => {
  beforeEach(() => {
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
  });

  describe("assignment target field visibility (K5)", () => {
    it("self-assigned needs neither an assignee nor a position", () => {
      const visible = global.TaskForm.visibleFieldsFor("SelfAssigned");
      expect(visible.assignee).toBe(false);
      expect(visible.poolPosition).toBe(false);
    });

    it("person-assigned needs an assignee but never a pool position", () => {
      const visible = global.TaskForm.visibleFieldsFor("Person");
      expect(visible.assignee).toBe(true);
      expect(visible.poolPosition).toBe(false);
    });

    it("pool-assigned needs a position and NO assignee", () => {
      // A pool task has no holder until it is claimed — offering an assignee field would contradict that.
      const visible = global.TaskForm.visibleFieldsFor("PositionPool");
      expect(visible.assignee).toBe(false);
      expect(visible.poolPosition).toBe(true);
    });

    it("an unknown target falls back to the safest shape", () => {
      const visible = global.TaskForm.visibleFieldsFor("Whatever");
      expect(visible.assignee).toBe(false);
      expect(visible.poolPosition).toBe(false);
    });
  });

  describe("position label (K4)", () => {
    it("shows the organization unit so two facilities are distinguishable", () => {
      const a = { positionId: "1", positionName: "QA Specialist", organizationUnitName: "Facility A" };
      const b = { positionId: "2", positionName: "QA Specialist", organizationUnitName: "Facility B" };

      expect(global.TaskForm.formatPositionLabel(a)).toBe("QA Specialist — Facility A");
      expect(global.TaskForm.formatPositionLabel(b)).toBe("QA Specialist — Facility B");
      // The labels must differ, otherwise the picker cannot prevent the wrong-facility mistake.
      expect(global.TaskForm.formatPositionLabel(a)).not.toBe(global.TaskForm.formatPositionLabel(b));
    });

    it("falls back to codes when names are absent", () => {
      expect(global.TaskForm.formatPositionLabel({ positionCode: "QA-A", organizationUnitCode: "FAC-A" }))
        .toBe("QA-A — FAC-A");
    });

    it("renders options carrying the unit label", () => {
      document.body.innerHTML = '<select id="pos"></select>';
      const select = document.getElementById("pos");

      global.TaskForm.renderPositionOptions(select, [
        { positionId: "1", positionName: "QA Specialist", organizationUnitName: "Facility A" },
        { positionId: "2", positionName: "QA Specialist", organizationUnitName: "Facility B" }
      ]);

      const labels = [...select.options].map((o) => o.textContent);
      expect(labels).toEqual(["QA Specialist — Facility A", "QA Specialist — Facility B"]);
    });
  });

  describe("create payload", () => {
    const baseDraft = {
      title: "  Prepare filing  ",
      priority: "High",
      dueAt: "2026-08-01",
      tags: "alpha, beta ,, gamma"
    };

    it("never sends lifecycle or spentHours — the server owns both", () => {
      const payload = global.TaskForm.buildCreatePayload({ ...baseDraft, assignmentTarget: "SelfAssigned" });
      expect(payload).not.toHaveProperty("lifecycle");
      expect(payload).not.toHaveProperty("spentHours");
      expect(payload).not.toHaveProperty("status");
    });

    it("trims the title and normalizes tags", () => {
      const payload = global.TaskForm.buildCreatePayload({ ...baseDraft, assignmentTarget: "SelfAssigned" });
      expect(payload.title).toBe("Prepare filing");
      expect(payload.tags).toEqual(["alpha", "beta", "gamma"]);
    });

    it("sends only the assignment field that belongs to the target", () => {
      const person = global.TaskForm.buildCreatePayload({
        ...baseDraft, assignmentTarget: "Person",
        assigneeUserId: "11111111-1111-1111-1111-111111111111",
        poolPositionId: "99999999-9999-9999-9999-999999999999"
      });
      expect(person.assigneeUserId).toBe("11111111-1111-1111-1111-111111111111");
      expect(person.poolPositionId).toBeNull();

      const pool = global.TaskForm.buildCreatePayload({
        ...baseDraft, assignmentTarget: "PositionPool",
        assigneeUserId: "11111111-1111-1111-1111-111111111111",
        poolPositionId: "99999999-9999-9999-9999-999999999999"
      });
      // A stray assignee on a pool task is rejected server-side, so it must be stripped here.
      expect(pool.assigneeUserId).toBeNull();
      expect(pool.poolPositionId).toBe("99999999-9999-9999-9999-999999999999");
    });

    it("drops the approval manager when approval is not requested", () => {
      const payload = global.TaskForm.buildCreatePayload({
        ...baseDraft, assignmentTarget: "SelfAssigned",
        approvalRequired: false, approvalManagerUserId: "11111111-1111-1111-1111-111111111111"
      });
      expect(payload.approvalManagerUserId).toBeNull();
    });

    it("defaults email notifications on and coerces non-numeric estimates to null", () => {
      const payload = global.TaskForm.buildCreatePayload({
        ...baseDraft, assignmentTarget: "SelfAssigned", estimateHours: "abc"
      });
      expect(payload.emailNotificationsEnabled).toBe(true);
      expect(payload.estimateHours).toBeNull();
    });
  });

  describe("validation mirrors the server rules", () => {
    it("requires a title and a due date for every target", () => {
      ["SelfAssigned", "Person", "PositionPool"].forEach((target) => {
        const result = global.TaskForm.validateDraft({ assignmentTarget: target });
        expect(result.valid).toBe(false);
        expect(result.errors).toContain("title");
        expect(result.errors).toContain("dueAt");
      });
    });

    it("requires an assignee for a person task and a position for a pool task", () => {
      const person = global.TaskForm.validateDraft({
        assignmentTarget: "Person", title: "t", dueAt: "2026-08-01"
      });
      expect(person.errors).toContain("assigneeUserId");

      const pool = global.TaskForm.validateDraft({
        assignmentTarget: "PositionPool", title: "t", dueAt: "2026-08-01"
      });
      expect(pool.errors).toContain("poolPositionId");
    });

    it("accepts a complete self-assigned draft", () => {
      const result = global.TaskForm.validateDraft({
        assignmentTarget: "SelfAssigned", title: "t", dueAt: "2026-08-01"
      });
      expect(result.valid).toBe(true);
    });
  });

  describe("quick ↔ detailed draft continuity (K9 / DEV-1)", () => {
    it("hands the quick draft to the detailed form without losing anything", () => {
      const quick = {
        title: "Quick one",
        assignmentTarget: "PositionPool",
        poolPositionId: "99999999-9999-9999-9999-999999999999",
        dueAt: "2026-08-05",
        priority: "High"
      };

      global.TaskForm.writeDraft(quick);
      const restored = global.TaskForm.readDraft();

      expect(restored).toEqual(quick);
    });

    it("clears the draft once it has been used", () => {
      global.TaskForm.writeDraft({ title: "x" });
      global.TaskForm.clearDraft();
      expect(global.TaskForm.readDraft()).toBeNull();
    });

    it("survives storage being unavailable instead of throwing", () => {
      const broken = {
        getItem() { throw new Error("blocked"); },
        setItem() { throw new Error("blocked"); },
        removeItem() { throw new Error("blocked"); }
      };

      expect(() => global.TaskForm.writeDraft({ title: "x" }, broken)).not.toThrow();
      expect(global.TaskForm.readDraft(broken)).toBeNull();
      expect(() => global.TaskForm.clearDraft(broken)).not.toThrow();
    });
  });

  describe("DOM visibility toggling uses classes, not inline styles (FG-003)", () => {
    it("hides and disables the fields that do not apply", () => {
      document.body.innerHTML = `
        <div data-task-field="assignee"><input id="a" /></div>
        <div data-task-field="poolPosition"><select id="p"></select></div>
        <div data-task-field="organizationUnit"><input id="u" /></div>`;

      global.TaskForm.applyTargetVisibility(document, "PositionPool");

      const assignee = document.querySelector('[data-task-field="assignee"]');
      const pool = document.querySelector('[data-task-field="poolPosition"]');

      expect(assignee.classList.contains("d-none")).toBe(true);
      expect(pool.classList.contains("d-none")).toBe(false);
      expect(document.getElementById("a").disabled).toBe(true);
      expect(document.getElementById("p").disabled).toBe(false);

      // No inline style was written anywhere.
      expect(assignee.getAttribute("style")).toBeNull();
      expect(pool.getAttribute("style")).toBeNull();
    });
  });
});
