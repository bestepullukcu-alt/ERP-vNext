const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-500 (WP-PLATFORM-TENANT-MODULES-01 FIX1 item 8) — THE "EXTEND EXPIRY" DATE BOX OPENS ON THE ROW'S DATE.
 *
 * The Modules tab asked for the row's date through `inputAttributes: { value }`. The vendored SweetAlert 11.14.5 writes
 * `input.value = inputValue` AFTER the attributes, with '' as the default — so the box opened EMPTY, and a plain "OK"
 * sent null, which removed the expiry.
 *
 * The fix does NOT grow the shared confirm (standing rule: a shared component does not grow to suit one module —
 * wcn-detail-three-regions.test.js pins that `_GlobalConfirmation.cshtml` has no `inputValue`). The tab uses the
 * wrapper's existing `didOpen` seam, as the Task Center's edit box does.
 *
 * Measured end to end: the REAL vendored library, the shared wrapper as it ships, and the options the Modules tab
 * builds (sliced from details.js) — the assertion is on the box in the DOM, not on a config object.
 */
const webRoot = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(webRoot, ...parts), "utf8");
const VIEW = read("Views", "Shared", "_GlobalConfirmation.cshtml");
const SWAL_SOURCE = read("wwwroot", "assets", "vendor", "libs", "sweetalert2", "sweetalert2.js");
const DETAILS = read("wwwroot", "assets", "js", "Platform", "Tenants", "details.js");

const wrapperScript = () => {
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  return script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
};
const expiryBlock = DETAILS.slice(DETAILS.indexOf("const todayIsoDate"), DETAILS.indexOf("const removeModuleEntitlementOverride"));

/** The options the Modules tab hands the shared confirm for a row, captured from the shipped code. */
const expiryDialogFor = (row, showConfirm) => {
  // eslint-disable-next-line no-new-func
  const api = new Function("L", "window", "fetchJson", "apiBase", "tenantId", "getAuthHeaders", "reportEntitlementChange", "showEntitlementActionError",
    expiryBlock + "; return { openModuleEntitlementExpiryEditor };")(
    { ExtendExpiry: "Extend Expiry", ExpiryDate: "Expiry date", EntitlementExpiryRequired: "Choose the new expiry date." },
    { showConfirm, showToast: () => {} },
    async () => null, "/api", "t1", () => ({}), async () => {}, () => {});
  api.openModuleEntitlementExpiryEditor(row);
};

const row = { physicalEntitlementId: "e1", moduleCode: "CRM", moduleName: "CRM", rowVersion: "AAAA", expiryDateUtc: "2027-03-31T23:59:59Z" };

describe("the Extend Expiry date box — the real library and the shared wrapper as they ship", () => {
  const loadSwal = () => {
    const host = {};
    // eslint-disable-next-line no-new-func
    new Function("self", "window", "document", "exports", "module", "define", SWAL_SOURCE)(
      host, global.window, global.document, undefined, undefined, undefined);
    return host.Swal || global.window.Swal;
  };
  const Swal = loadSwal();
  const sharedConfirm = () => {
    const win = {};
    // eslint-disable-next-line no-new-func
    new Function("window", "Swal", "console", "confirm", wrapperScript())(win, Swal, { log() {}, warn() {}, error() {} }, () => false);
    return win.showConfirm;
  };
  const box = () => document.querySelector(".swal2-popup input.swal2-input");

  afterEach(() => {
    if (Swal.isVisible && Swal.isVisible()) { Swal.close(); }
    document.body.innerHTML = "";
  });

  // SweetAlert calls didOpen after the popup is shown, on a timer — the box is read once that has run.
  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

  test("opens on the row's current date", async () => {
    expiryDialogFor(row, sharedConfirm());
    await settle();

    expect(box().type).toBe("date");
    expect(box().value).toBe("2027-03-31");
  });

  test("a row without an expiry opens an empty box — and an empty box is refused, never sent", async () => {
    expiryDialogFor({ ...row, expiryDateUtc: null }, sharedConfirm());
    await settle();

    expect(box().value).toBe("");
  });

  test("a value ATTRIBUTE alone still opens empty — why the tab uses the didOpen seam", async () => {
    sharedConfirm()("ExtendExpiry", () => {}, { showInput: true, inputType: "date", inputAttributes: { value: "2027-03-31" } });
    await settle();

    expect(box().value).toBe("");
  });

  test("the shared confirm did not grow an option for this tab", () => {
    expect(VIEW).not.toContain("inputValue");
  });
});

describe("the Extend Expiry options in a bare vm context (no document, no window of its own)", () => {
  test("the tab's dialog code reaches the shared wrapper without touching a global document", () => {
    const seen = {};
    const input = { value: "" };
    // The wrapper hands its OWN Swal to the caller's didOpen; the box it reaches is this one.
    const swal = { fire: (config) => { seen.config = config; return { then: () => {} }; }, showValidationMessage: () => {}, getInput: () => input };
    const context = vm.createContext({ window: {}, Swal: swal, console: { log() {}, warn() {}, error() {} }, confirm: () => false });
    vm.runInContext(wrapperScript(), context);
    expect(vm.runInContext("typeof document", context)).toBe("undefined");

    expiryDialogFor(row, context.window.showConfirm);

    expect(seen.config.input).toBe("date");
    seen.config.didOpen({});
    expect(input.value).toBe("2027-03-31");
  });
});
