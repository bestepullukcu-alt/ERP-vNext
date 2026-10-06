const fs = require("fs");
const path = require("path");

/*
 * CT acceptance (calendar 2c review, 2026-09-29): the native `confirm()` fallback of window.showConfirm called the
 * callback on Yes but never `onCancel` on No — the Swal path does. A caller that WAITS for the answer (an
 * invitation's Accept over a planned block) then hung forever with its in-flight guard held. Executed from the view
 * itself, the way global-confirm-input-type.test.js does.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const VIEW = fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", "Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");

const loadWithoutSwal = (answer) => {
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const win = {};
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(win, undefined, console, () => answer);
  return win.showConfirm;
};

describe("showConfirm without Swal (the native confirm fallback)", () => {
  it("No calls onCancel and not the callback; Yes calls the callback and not onCancel", () => {
    const seen = [];
    loadWithoutSwal(false)("AreYouSure", () => seen.push("yes"), { onCancel: () => seen.push("no") });
    expect(seen, "No on the native dialog must answer the caller").toEqual(["no"]);

    seen.length = 0;
    loadWithoutSwal(true)("AreYouSure", () => seen.push("yes"), { onCancel: () => seen.push("no") });
    expect(seen).toEqual(["yes"]);
  });
});
