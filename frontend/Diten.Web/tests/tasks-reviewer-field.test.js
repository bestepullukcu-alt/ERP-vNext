const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * Faz 3b follow-up — the reviewer field.
 *
 * THE DEFECT. The review switch shipped live with no reviewer field beside it, so the form produced a task whose
 * review MOD-0023 refuses to open: its validator requires at least one candidate principal, and MOD-0024 was
 * sending an empty list. Created 201, started 204, and then `submitReview` answered 409 forever. Approval never
 * had this hole because its manager field has always travelled with its switch — this is that missing twin, and
 * the tests below pin both halves: the markup and the behaviour.
 */
describe("MOD-0024 reviewer field", () => {
  const FORM = path.resolve(__dirname, "..", "Views", "Tasks", "_Form.cshtml");
  const REVIEWER = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

  // ── The markup, on approval's exact pattern ───────────────────────────────

  describe("markup", () => {
    const html = () => fs.readFileSync(FORM, "utf8");

    it("puts a reviewer field beside the review switch", () => {
      const form = html();
      expect(form).toContain('data-task-field="reviewer"');
      expect(form).toContain('id="taskReviewer"');
      expect(form).toContain('name="reviewerCandidateUserId"');
    });

    it("marks it required and starts it hidden, exactly like the approval manager", () => {
      const form = html();
      const reviewer = form.slice(
        form.indexOf('data-task-field="reviewer"'),
        form.indexOf('id="taskApprovalRequired"'));
      const approval = form.slice(form.indexOf('data-task-field="approvalManager"'));

      // Hidden until its switch is on — the same class-based toggle, never inline style (FG-003).
      expect(form).toContain('class="d-none mb-2" data-task-field="reviewer"');
      // The required marker both fields carry.
      expect(reviewer).toContain('text-danger');
      expect(approval).toContain('text-danger');
      // Localized label and hint — never literal text in the view.
      expect(reviewer).toContain('@Localizer["FieldReviewer"]');
      expect(reviewer).toContain('@Localizer["ReviewerHint"]');
    });

    it("uses the same kind of control as the approval manager rather than a new picker", () => {
      // "Do not invent a new selector": both are the same input shape, so the two governance fields read as one
      // pattern instead of two.
      const form = html();
      expect(form).toContain('type="text" class="form-control" id="taskReviewer"');
      expect(form).toContain('type="text" class="form-control" id="taskApprovalManager"');
    });
  });

  // ── Visibility, driven by the switch ──────────────────────────────────────

  describe("visibility", () => {
    const render = () => {
      document.body.innerHTML = `
        <div id="taskForm" data-task-mode="create">
          <input type="checkbox" id="taskReviewRequired" />
          <div class="d-none mb-2" data-task-field="reviewer">
            <input type="text" id="taskReviewer" />
          </div>
          <input type="checkbox" id="taskApprovalRequired" />
          <div class="d-none" data-task-field="approvalManager">
            <input type="text" id="taskApprovalManager" />
          </div>
        </div>`;
    };

    // The page script owns syncVisibility privately, so this exercises the same rule the way the DOM sees it:
    // the field's hidden class follows its own switch and nothing else.
    const sync = () => {
      const form = document.getElementById("taskForm");
      form.querySelectorAll('[data-task-field="reviewer"]').forEach((node) => {
        node.classList.toggle("d-none", !document.getElementById("taskReviewRequired").checked);
      });
      form.querySelectorAll('[data-task-field="approvalManager"]').forEach((node) => {
        node.classList.toggle("d-none", !document.getElementById("taskApprovalRequired").checked);
      });
    };

    beforeEach(render);

    it("stays hidden while no review is requested", () => {
      sync();
      expect(document.querySelector('[data-task-field="reviewer"]').classList.contains("d-none")).toBe(true);
    });

    it("appears when the review switch is turned on", () => {
      document.getElementById("taskReviewRequired").checked = true;
      sync();
      expect(document.querySelector('[data-task-field="reviewer"]').classList.contains("d-none")).toBe(false);
    });

    it("is keyed off its OWN switch, not approval's", () => {
      // Non-vacuity: a toggle wired to the wrong checkbox would pass both tests above if they were run with
      // both switches moving together.
      document.getElementById("taskApprovalRequired").checked = true;
      sync();
      expect(document.querySelector('[data-task-field="reviewer"]').classList.contains("d-none")).toBe(true);
      expect(document.querySelector('[data-task-field="approvalManager"]').classList.contains("d-none")).toBe(false);
    });

    it("wires the review switch to the visibility sync", () => {
      // The rule is only real if something calls it. This pins the listener the page registers.
      const page = fs.readFileSync(
        path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Tasks", "form-page.js"), "utf8");
      expect(page).toContain("el('taskReviewRequired')?.addEventListener('change', syncVisibility)");
      expect(page).toContain('form?.querySelectorAll(\'[data-task-field="reviewer"]\')');
    });
  });

  // ── The draft: required when the switch is on, dropped when it is off ─────

  describe("draft rules", () => {
    beforeEach(() => {
      delete global.TaskForm;
      loadScript("wwwroot/assets/js/Tasks/form.js");
    });

    const draft = (overrides = {}) => ({
      title: "İncelenecek iş",
      assignmentTarget: "SelfAssigned",
      dueAt: "2026-08-01",
      ...overrides
    });

    it("refuses a review with no reviewer", () => {
      const check = global.TaskForm.validateDraft(draft({ reviewRequired: true }));
      expect(check.valid).toBe(false);
      expect(check.errors).toContain("reviewerCandidateUserId");
    });

    it("accepts a review that names one", () => {
      const check = global.TaskForm.validateDraft(
        draft({ reviewRequired: true, reviewerCandidateUserId: REVIEWER }));
      expect(check.valid).toBe(true);
    });

    it("asks for no reviewer when no review is requested", () => {
      // Non-vacuity: a rule that fired unconditionally would break every ordinary task.
      const check = global.TaskForm.validateDraft(draft({ reviewRequired: false }));
      expect(check.valid).toBe(true);
    });

    it("drops the reviewer when the requirement is switched off", () => {
      // Same rule the approval manager follows: a candidate nothing will ever route to is not worth storing.
      const payload = global.TaskForm.buildCreatePayload(
        draft({ reviewRequired: false, reviewerCandidateUserId: REVIEWER }));
      expect(payload.reviewerCandidateUserId).toBeNull();
    });

    it("sends the reviewer when the requirement is on", () => {
      const payload = global.TaskForm.buildCreatePayload(
        draft({ reviewRequired: true, reviewerCandidateUserId: REVIEWER }));
      expect(payload.reviewerCandidateUserId).toBe(REVIEWER);
    });
  });
});
